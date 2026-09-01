# Gói 4 — nửa CODE: `ICacheService` · `ShutdownTimeout` · DataProtection → SSM

**Ngày:** 2026-09-01 · **Nhánh:** `fix/muc-I-client-error-leaks`
**Trạng thái:** nửa code XONG và ĐÃ ĐO · **nửa hạ tầng CHƯA LÀM — chờ review**

## Cái gì đã đo, và vì sao build sạch không đủ

`dotnet build` không chứng minh được điều nào trong bảng dưới. Cả bốn dòng đều cần chạy thật.

| Ca | Điều kiện | Kỳ vọng | Kết quả |
|---|---|---|---|
| **A** | không có `DataProtection:SsmPrefix` | API khởi động bình thường | ✅ `health/live` = 200 |
| **A** | `GetOrCreateAsync` gọi 2 lần cùng khoá | factory chạy **1** lần | ✅ `soLanGoiFactory = 1` |
| **A** | `GetAsync` khoá không tồn tại · `GetAsync` sau `RemoveAsync` | `null` cả hai | ✅ `(null)` · `(null)` |
| **A** | `HostOptions.ShutdownTimeout` | 45 giây | ✅ `shutdownTimeoutGiay: 45` |

Dòng cuối là dòng đáng giá nhất: nó chứng minh option **thật sự được bind**, chứ không phải
"đã viết một dòng `Configure<HostOptions>`".

Đo bằng hai endpoint tạm (`/__probe/cache`, `/__probe/dp`), **đã gỡ sau khi đo**
(`grep __PROBE src/API/Program.cs` = 0).

---

## 🔴 Phát hiện quyết định cho nửa hạ tầng — có ca đối chứng

Câu hỏi: nếu Terraform đặt `DataProtection__SsmPrefix` **trước khi** cấp quyền cho task role
thì chuyện gì xảy ra? Suy luận không trả lời được — SSM có thể đọc lười, hoặc app có thể âm
thầm rơi về key tạm. Đo:

| Ca | `SsmPrefix` | Quyền AWS | App khởi động | `IDataProtector.Protect` |
|---|---|---|---|---|
| **B** | có | **không** | ✅ **vẫn lên** | 🔴 `CryptographicException` |
| **C** | không | — | ✅ | ✅ `"xin-chao"` giải mã đúng |

Log ở ca B, hai dòng liền nhau:

```
fail: Amazon.AspNetCore.DataProtection.SSM.SSMXmlRepository[0]
      Error calling SSM to get parameters starting with /hushstore/prod/dataprotection/:
      The security token included in the request is invalid.
fail: Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingProvider[48]
      An error occurred while reading the key ring.
```

**Ba kết luận, không cái nào suy ra được mà không đo:**

1. **App VẪN khởi động** dù key ring hỏng. Nên `health/live` xanh, ECS coi task là healthy,
   ALB đưa traffic vào — và deploy "thành công".
2. **Nhưng nó hỏng ẦM Ĩ, không âm thầm.** `Protect` ném `CryptographicException` → 500. Đây
   là tin **tốt**: nó tốt hơn hẳn phương án âm thầm dùng key tạm, vì suy giảm âm thầm sẽ tái
   tạo đúng bug ta đang sửa mà không ai thấy.
3. **Ranh giới hỏng rất hẹp và rất khó nhận ra:** chỉ những đường dùng DataProtection mới
   500 — **link đặt lại mật khẩu, link xác nhận email, antiforgery token**. Trang chủ, danh
   sách sản phẩm, đăng nhập, đặt hàng đều **bình thường**. Không dashboard nào đỏ.

> 🚨 **Hệ quả bắt buộc cho nửa hạ tầng: biến môi trường và chính sách IAM phải vào CÙNG MỘT
> lần deploy.** Đặt env var trước IAM là tự tạo một cửa sổ mà chức năng đặt lại mật khẩu chết
> trong khi mọi tín hiệu sức khoẻ đều xanh.

---

## 🔴 Lỗi THẬT đang được sửa, không phải dọn dẹp

Mặc định ASP.NET Core sinh key ring vào **ổ đĩa của từng container**. Với 2 task:

- Khách bấm "quên mật khẩu" → task **A** phát hành link
- Khách bấm link → ALB định tuyến sang task **B** → **B không giải mã được** → link "không hợp lệ"

Triệu chứng đúng loại khó nhất: *"lúc được lúc không"*, và **không tái hiện được trên máy một
tiến trình** — cùng họ với lỗi cache phân quyền của đợt 1.

Cùng lý lẽ cho `ShutdownTimeout`: hiện `deregistration_delay = 5` và ShutdownTimeout mặc định
30 giây chạy **song song**, biên = **0**. Request đang bay có thể bị cắt giữa dòng lúc rolling
deploy.

---

## 🔴 Chệch khỏi kế hoạch gốc một cách có ý thức — gói Redis

Kế hoạch đợt 4 nêu mục tiêu: *"đợt 8 chỉ là bật một biến Terraform, không phải sửa code"*,
tức thêm sẵn `Microsoft.Extensions.Caching.StackExchangeRedis` **ngay bây giờ**.

**Không làm vậy.** Lý do là bài học đắt nhất của mục D: gói `AutoMapper` từng nằm trong
`API.csproj` và `Service.csproj` mà **không một dòng code nào dùng** — và nó mang một lỗ hổng
**High** (`GHSA-rvv3-g6hj-g44x`). Một gói chưa dùng không phải "chuẩn bị trước", nó là một
khoản nợ bảo mật nằm im; cổng `check-vulnerable-packages.sh` ra đời chính vì chuyện đó.

**Cái giá của việc hoãn, đo rõ:** đợt 8 phải thêm 1 `PackageReference` và đổi 1 dòng thành
`AddStackExchangeRedisCache(...)`. Hai dòng. Đổi lại: không mang một gói không dùng qua bốn
đợt. **`ICacheService` và mọi chỗ gọi nó KHÔNG đổi** — đó mới là phần kế hoạch thực sự muốn
bảo vệ, và nó đã được bảo vệ.

---

## 🚨 Xung đột trong kế hoạch, cần bạn quyết ở nửa hạ tầng

Tiêu chí XONG của gói 4 trong runbook ghi: *"`plan` sạch, build sạch, **6 file `.tftest.hcl`
chưa đụng tới**"*. Nhưng `deregistration_delay 5 → 30` là việc của đợt **4**, và
[`alb.tftest.hcl:167-170`](../../infra/tf/modules/alb/tests/alb.tftest.hcl#L167-L170) khẳng định
thẳng:

```hcl
tostring(aws_lb_target_group.web[0].deregistration_delay) == "5",
tostring(aws_lb_target_group.api[0].deregistration_delay) == "5",
error_message = "deregistration_delay phải là 5s: topology static host port + max_size 1
                 buộc stop-rồi-start nên draining chỉ kéo dài downtime."
```

Hai tiêu chí này **không thể cùng đúng**. Và lưu ý: repo có **12** file `.tftest.hcl`, không
phải 6 — con số 6 trong kế hoạch chưa được kiểm.

Test đó là **hợp đồng, không phải nhiễu**: lý lẽ của nó (`max_size 1` ⇒ draining chỉ kéo dài
downtime) đúng **tại thời điểm viết**. Đợt 5 mới nâng `max_size`. Nên có ba lối:

1. **Đổi cả số lẫn test ở gói 4, và viết lại `error_message`** để nói rõ điều kiện đã đổi.
2. **Hoãn `deregistration_delay` sang gói 5**, nơi `max_size` được nâng — giữ ba số shutdown
   thành một thay đổi nguyên khối cùng autoscale.
3. Đổi số ở gói 4, `-target` test đó cho pass tạm — **không khuyến nghị**, đó là tắt hợp đồng.

⚠️ Lối 1 để lại một cửa sổ mà `ShutdownTimeout = 45` **lớn hơn** `stopTimeout` mặc định 30 giây
của ECS, nghĩa là ECS `SIGKILL` container trong lúc nó còn đang drain — **xấu hơn hiện tại**.
Nếu chọn lối 1, `stopTimeout = 90` phải vào **cùng** lần deploy đó.

## Nửa hạ tầng — CHƯA LÀM, chờ review

| Việc | Vị trí | Hiện tại | Đích |
|---|---|---|---|
| `deregistration_delay` | `modules/alb/alb.tf:58`, `:83` | `5` | `30` |
| ECS `stopTimeout` | `modules/ecs/taskdef.tf` container `api` + `web` | không khai | `90` |
| `ECS_CONTAINER_STOP_TIMEOUT` | `modules/ecs/user_data.sh.tftpl:26` | `30s` | `90s` ⚠️ thay instance |
| Connection pool | `modules/data/main.tf:126-134` | không có (mặc định 100/tiến trình) | `Max Pool Size=30; Min Pool Size=2; Connect Timeout=15` |
| IAM task role | `modules/ecs/iam.tf` | chưa có | `ssm:GetParametersByPath` + `PutParameter` trên `/hushstore/prod/dataprotection/*` |
| Env var | taskdef container `api` | chưa có | `DataProtection__SsmPrefix` |

## Cổng đã chạy

`dotnet build --no-incremental` = **0 Error(s), 184 Warning(s)** — đúng baseline, thay đổi này
thêm **0** cảnh báo mới (bản đầu thêm 1 do `using` trùng, đã sửa) ·
`check-error-message-leaks.sh all` = Sạch 140 file, 0 chỗ ·
`check-vulnerable-packages.sh` = Sạch 7 project (đã chạy **sau** khi thêm
`Amazon.AspNetCore.DataProtection.SSM` 4.0.3) ·
LoadProbe `S02,S05,S03,S06` = **4 ĐẠT, 0 hỏng, 0 không kết luận** — DI mới không hồi quy.

⚠️ **Một nhiễu do môi trường, ghi lại để không ai đọc sai log:** giữa lúc đo, Docker Desktop
tự tắt, nên `dpB.log` có lỗi `SqlException ... server was not found`. Đó **không** phải lỗi
sản phẩm — nó ở dòng log **thứ 2**, trước cả `Now listening on`. Đã dựng lại Docker và chạy
lại chốt hồi quy để xác nhận.
