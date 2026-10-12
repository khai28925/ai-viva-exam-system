# Setup PostgreSQL cho thành viên

Mỗi thành viên chạy **database local riêng**. Nhóm thống nhất PostgreSQL 16, schema từ EF Core migrations trong repo và chuỗi kết nối `ConnectionStrings__QuestionBank`. **Docker Compose là cách khuyến nghị**, không bắt buộc. Có thể cài PostgreSQL trực tiếp hoặc dùng dịch vụ PostgreSQL khác nếu tương thích và chạy được cùng migrations. DBeaver chỉ là công cụ xem/chỉnh dữ liệu, không phải thành phần của ứng dụng; `psql`, pgAdmin hoặc client khác đều dùng được.

Không dùng chung database đang làm việc của một thành viên cho cả nhóm: migration, dữ liệu thử và thao tác xóa có thể ảnh hưởng nhau. Không commit mật khẩu, connection string thật hoặc database dump chứa dữ liệu nhạy cảm.

## Chuẩn chung trước khi chọn cách cài

- Cài [.NET SDK theo `global.json`](../global.json) (`10.0.401` hoặc patch tương thích) và kiểm tra `dotnet --version`.
- Dùng PostgreSQL **16** cho môi trường local để khớp image trong [`backend/compose.postgres.yml`](../backend/compose.postgres.yml). Tạo database `aives`, user `aives` có mật khẩu riêng trên máy mình và quyền sở hữu database. Port ví dụ là `5432`; nếu máy dùng port khác, thay port trong chuỗi kết nối.
- Luôn chạy migrations có sẵn bằng `dotnet ef database update`. **Không** tự tạo các bảng bằng tay hoặc import ERD thành schema. Chỉ người phụ trách database thêm migration mới và review trong PR.
- API đọc `ConnectionStrings__QuestionBank` từ biến môi trường. API **không** tự chạy migration khi khởi động.

Các ví dụ nối chuỗi bên dưới phù hợp với mật khẩu local dài, ngẫu nhiên bằng chữ và số. Nếu mật khẩu có dấu `;` hoặc ký tự đặc biệt làm hỏng cú pháp connection string, đặt giá trị `Password` trong dấu ngoặc kép theo [quy tắc Npgsql](https://www.npgsql.org/doc/connection-string-parameters) thay vì đổi mật khẩu thành loại ngắn/dễ đoán.

Các lệnh dưới đây viết cho **PowerShell**. Chạy từ thư mục `backend/` của repo, trừ khi có ghi khác.

## Cách A — Docker Compose (khuyến nghị)

Cần Docker Desktop/Engine và Docker Compose hoạt động. Mở PowerShell mới tại `backend/`:

```powershell
$pgPassword = Read-Host 'Mật khẩu PostgreSQL local cho AIVES' -AsSecureString
$env:AIVES_POSTGRES_PASSWORD = [System.Net.NetworkCredential]::new('', $pgPassword).Password
docker compose -f compose.postgres.yml up -d --wait
$env:ConnectionStrings__QuestionBank = "Host=localhost;Port=5432;Database=aives;Username=aives;Password=$env:AIVES_POSTGRES_PASSWORD"
```

Nếu `up --wait` thành công, chuyển sang mục **Chạy migration và kiểm tra**. Compose chỉ mở database ở `127.0.0.1:5432`; nó không chạy backend hoặc frontend. Mật khẩu ở trên **chỉ được áp dụng khi volume PostgreSQL được khởi tạo lần đầu**. Nếu đã có volume mà quên mật khẩu, đổi mật khẩu trong PostgreSQL và cập nhật chuỗi kết nối; đừng xóa volume để “sửa nhanh” vì sẽ mất dữ liệu local.

Lần sau mở Docker và chạy lại `docker compose -f compose.postgres.yml up -d --wait`. Cần nhập lại cùng mật khẩu để Compose đọc cấu hình, nhưng mật khẩu của database vẫn là giá trị đã được khởi tạo/đổi trong volume. Nếu port `5432` đang được PostgreSQL khác dùng, không chạy hai server trên cùng port; dùng cách B với port khác hoặc thống nhất đổi cấu hình local trước khi khởi động.

## Cách B — PostgreSQL cài trực tiếp, không dùng Docker

Cài và khởi động PostgreSQL 16. Dùng tài khoản quản trị local của PostgreSQL để tạo role/database. Nếu các CLI `createuser` và `createdb` đã có trong `PATH`, chạy từ PowerShell (thay `5432` nếu server local dùng port khác):

```powershell
createuser -h localhost -p 5432 -U postgres --pwprompt aives
createdb -h localhost -p 5432 -U postgres -O aives aives
```

`createuser --pwprompt` hỏi mật khẩu **mới cho `aives`**; khi kết nối, công cụ cũng có thể hỏi mật khẩu của admin `postgres`. Nếu CLI chưa ở `PATH`, chạy từ thư mục `bin` của PostgreSQL hoặc tạo role `aives` và database `aives` (owner `aives`) bằng GUI quản trị. Nếu role/database đã tồn tại, không tạo lại và không xóa dữ liệu; chỉ xác nhận quyền truy cập.

Mở PowerShell tại `backend/`, nhập mật khẩu **của user `aives`**, rồi đặt chuỗi kết nối. Port phải là port thực tế của PostgreSQL trên máy:

```powershell
$pgPassword = Read-Host 'Mật khẩu user aives' -AsSecureString
$env:ConnectionStrings__QuestionBank = "Host=localhost;Port=5432;Database=aives;Username=aives;Password=$([System.Net.NetworkCredential]::new('', $pgPassword).Password)"
```

Không dùng `Username=postgres` nếu đã tạo user `aives`; ứng dụng và migration phải trỏ **cùng một** database. Nếu dùng PostgreSQL từ máy khác, thay `Host`/`Port` theo thông tin được cấp và bảo đảm đường mạng, quyền truy cập, TLS phù hợp. Đừng dùng database production để phát triển hoặc demo.

## Chạy migration và kiểm tra (cả hai cách)

Trong **cùng PowerShell đang có** `ConnectionStrings__QuestionBank`:

```powershell
dotnet tool restore
dotnet restore AiVivaExamSystem.sln
dotnet ef database update --project src/AiViva.Infrastructure --startup-project src/AiViva.Infrastructure
dotnet ef migrations list --project src/AiViva.Infrastructure --startup-project src/AiViva.Infrastructure
dotnet run --project src/AiViva.Api
```

Migration thành công khi lệnh `database update` kết thúc không lỗi và `migrations list` không báo migration pending. API mặc định ở `http://localhost:5065`; kiểm tra `http://localhost:5065/api/health`. Health endpoint hiện chỉ kiểm tra API, **không chứng minh database kết nối được**. Để kiểm tra DB thật, đăng nhập và thử một API đọc Question Bank hoặc dùng client SQL chạy:

```sql
SELECT current_database(), current_user;
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
ORDER BY table_name;
```

Nếu cần tài khoản đăng nhập đầu tiên, xem phần **Đăng nhập và phân quyền MVP** trong [`backend/README.md`](../backend/README.md). Biến môi trường chỉ có hiệu lực trong terminal hiện tại; mở terminal mới thì đặt lại connection string. FE kết nối **API**, không kết nối PostgreSQL trực tiếp.

## Xem database bằng client bất kỳ

DBeaver là tùy chọn. Với Docker mặc định, điền `Host=localhost`, `Port=5432`, `Database=aives`, `Username=aives`, `Password=<mật khẩu AIVES trên máy bạn>`. Với PostgreSQL cài trực tiếp, dùng đúng host/port/user đã cấu hình. Trong DBeaver, mở connection → `Databases` → `aives` → `Schemas` → `public` → `Tables`; client khác cũng xem schema `public` hoặc chạy các câu SQL ở trên. Không dùng mật khẩu `postgres` cho kết nối user `aives`.

## Lỗi thường gặp

| Hiện tượng | Kiểm tra |
| --- | --- |
| `Connection refused` | PostgreSQL chưa chạy hoặc `Host`/`Port` sai; với Docker kiểm tra `docker compose -f compose.postgres.yml ps`. |
| `password authentication failed` | Kiểm tra username, mật khẩu của **đúng database instance** và chuỗi kết nối. Đổi biến `AIVES_POSTGRES_PASSWORD` không đổi mật khẩu trong volume cũ. |
| Port `5432` already in use | Đang có PostgreSQL khác trên máy. Xác định đúng instance; không xóa container/volume của dự án khác. |
| `relation ... does not exist` | Có thể API trỏ DB khác hoặc chưa chạy `dotnet ef database update` trên DB đó. |
| `permission denied` khi migration | User cần quyền tạo/sửa schema `public`; kiểm tra owner/quyền của database local. |
| Đã chạy migration nhưng client không thấy bảng | Client đang trỏ sai host/port/database hoặc chưa refresh schema `public`. |

Với Docker, `docker compose -f compose.postgres.yml stop` chỉ dừng container. **Không chạy `down --volumes`** trừ khi chủ động muốn xóa toàn bộ dữ liệu local và đã sao lưu.

Tham khảo: [EF Core áp dụng migration](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [PostgreSQL `createuser`](https://www.postgresql.org/docs/16/app-createuser.html), [`createdb`](https://www.postgresql.org/docs/16/app-createdb.html), [Docker Compose volumes](https://docs.docker.com/reference/compose-file/volumes/).
