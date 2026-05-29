using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
    public class PosScanRequest
    {
        [Required]
        public string SerialNumber { get; set; } = string.Empty;
    }
}
