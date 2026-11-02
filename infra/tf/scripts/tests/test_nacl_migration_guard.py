"""Run the real shell entry points with local Terraform/AWS stand-ins only."""

import os
from pathlib import Path
import shutil
import signal
import subprocess
import tempfile
import unittest


SCRIPTS = Path(__file__).resolve().parents[1]


class NaclMigrationGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.scripts = self.root / "scripts"
        shutil.copytree(SCRIPTS, self.scripts, ignore=shutil.ignore_patterns("tests"))
        self.prod = self.root / "envs/prod"
        (self.prod / ".terraform").mkdir(parents=True)
        self.tfvars = self.prod / "terraform.tfvars"
        self.original_tfvars = "enable_alb = true\nenable_nat = true\ninstance_count = 1\n"
        self.tfvars.write_text(self.original_tfvars)
        self.state = self.root / "state-list"
        self.state.write_text("")
        self.trace = self.root / "trace"
        self.bin = self.root / "bin"
        self.bin.mkdir()
        self.executable("terraform", '''#!/bin/bash
printf 'terraform %s\\n' "$*" >> "$TRACE"
if [ "$2" = state ] && [ "$3" = list ]; then
  cat "$STATE_LIST"
  exit "$STATE_RC"
fi
if [ "$2" = apply ]; then
  echo 'Apply complete! Resources: 0 added, 0 changed, 0 destroyed.'
  exit "$APPLY_RC"
fi
exit 99
''')
        self.executable("aws", '''#!/bin/bash
printf 'aws %s\\n' "$*" >> "$TRACE"
if [ "$1 $2" = 'sts get-caller-identity' ]; then
  echo '{}'
  exit 0
fi
exit 99
''')
        self.executable("jq", "#!/bin/bash\nexit 99\n")
        self.executable("sleep", "#!/bin/bash\nexit 0\n")
        self.env = dict(os.environ, PATH=f"{self.bin}:/usr/bin:/bin",
                        TRACE=str(self.trace), STATE_LIST=str(self.state), STATE_RC="0", APPLY_RC="99")

    def executable(self, name, content):
        path = self.bin / name
        path.write_text(content)
        path.chmod(0o755)

    def run_shell(self, command):
        with subprocess.Popen(["/bin/bash", "-c", command], env=self.env,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                              text=True, start_new_session=True) as process:
            try:
                stdout, stderr = process.communicate(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.communicate()
                raise
            return subprocess.CompletedProcess(process.args, process.returncode, stdout, stderr)

    def run_function(self, function):
        return self.run_shell(f'. "{self.scripts}/lib.sh"; {function}')

    def calls(self):
        return self.trace.read_text().splitlines() if self.trace.exists() else []

    def assert_blocked(self, result):
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn("docs/nacl-to-sg-migration.md", result.stderr)
        self.assertFalse(any(" apply " in call for call in self.calls()), self.calls())

    def test_each_remaining_legacy_acl_blocks_apply(self):
        # Even one remaining tier after a partial migration must stop apply.
        for tier in ("public", "app", "db"):
            with self.subTest(tier=tier):
                self.state.write_text(f"module.network.aws_network_acl.{tier}\n")
                self.assert_blocked(self.run_function('hs_apply "test"'))
        self.assertFalse((self.root / ".local").exists())

    def test_nested_and_indexed_custom_acls_block_apply(self):
        for address in ('module.stack.module.network.aws_network_acl.public[0]',
                        'module.stack["prod"].module.network.aws_network_acl.app["a"]',
                        'aws_network_acl.other'):
            with self.subTest(address=address):
                self.state.write_text(address + "\n")
                self.assert_blocked(self.run_function('hs_apply "test"'))

    def test_state_read_failure_blocks_apply_even_with_empty_output(self):
        self.env["STATE_RC"] = "1"
        self.assert_blocked(self.run_function('hs_apply "test"'))
        self.assertFalse((self.root / ".local").exists())

    def test_no_custom_acl_permits_apply_after_state_check(self):
        self.env["APPLY_RC"] = "0"
        for state in ("", "module.network.aws_default_network_acl.default\n"
                          "module.security.aws_security_group.web\n"):
            with self.subTest(state=state):
                self.trace.unlink(missing_ok=True)
                self.state.write_text(state)
                result = self.run_function('hs_apply "test"')
                self.assertEqual(result.returncode, 0, result.stderr)
                calls = self.calls()
                self.assertEqual(len(calls), 2, calls)
                self.assertTrue(calls[0].endswith(" state list"), calls)
                self.assertIn(" apply ", calls[1])

    def test_up_and_down_block_before_aws_or_tfvars_mutations(self):
        for script in ("up.sh", "down.sh"):
            for state_rc, state in (("0", "module.network.aws_network_acl.db\n"),
                                    ("1", "")):
                with self.subTest(script=script, state_rc=state_rc):
                    self.trace.unlink(missing_ok=True)
                    self.state.write_text(state)
                    self.env["STATE_RC"] = state_rc
                    result = self.run_shell(f'bash "{self.scripts}/{script}"')
                    self.assert_blocked(result)
                    calls = self.calls()
                    self.assertEqual(len(calls), 2, calls)
                    self.assertTrue(calls[0].startswith("aws sts get-caller-identity "), calls)
                    self.assertTrue(calls[1].endswith(" state list"), calls)
                    self.assertEqual(self.tfvars.read_text(), self.original_tfvars)
                    self.assertFalse((self.root / ".local").exists())


if __name__ == "__main__":
    unittest.main()
