-- ==============================================================================
-- ModMove: Script cập nhật nội dung mặc định 04 chính sách vào master_data_items
-- An toàn, không ghi đè dữ liệu nếu Admin đã nhập nội dung tùy chỉnh.
-- ==============================================================================

-- 1. CHÍNH SÁCH BẢO HÀNH (WARRANTY)
UPDATE `master_data_items`
SET `description` = '1. MỤC ĐÍCH VÀ PHẠM VI BẢO VỆ
ModMove cam kết bảo vệ tối đa quyền lợi và an toàn tài sản của Quý khách trong suốt quá trình vận chuyển đồ đạc, chuyển nhà, chuyển trọ. Mọi đồ đạc đều được kiểm soát bằng hình ảnh và có quy trình đền bù minh bạch.

2. QUY TRÌNH KIỂM HÀNG VÀ DUYỆT ẢNH TRƯỚC KHI XE LĂN BÁNH
- Khi bốc đồ lên xe: Tài xế bắt buộc chụp từ 02 đến 05 bức ảnh rõ nét hiện trạng đồ đạc đã xếp ngay ngắn trên thùng xe và gửi lên ứng dụng.
- Khách hàng duyệt ảnh (Bắt buộc): Chuyến xe CHỈ ĐƯỢC PHÉP BẮT ĐẦU DI CHUYỂN KHI KHÁCH HÀNG BẤM "ĐỒNG Ý" DUYỆT ẢNH TRÊN ỨNG DỤNG. Khách hàng có quyền yêu cầu tài xế sắp xếp lại nếu thấy chèn ép, nguy cơ đổ vỡ (tối đa 5 lần chụp ảnh).
- Bàn giao tại điểm đến: Tài xế chụp 01 bức ảnh xác nhận giao hàng thành công tại địa chỉ trả hàng làm căn cứ đối chiếu.

3. DANH MỤC HÀNG HÓA TUYỆT ĐỐI CẤM VẬN CHUYỂN
ModMove từ chối vận chuyển các mặt hàng sau:
- Vũ khí, vật liệu nổ, đạn dược, pháo các loại.
- Ma túy, chất kích thích, tiền chất ma túy, thuốc lá lậu.
- Hóa chất độc hại, chất ăn mòn, chất dễ cháy nổ không có bao bì chuyên dụng.
- Động vật sống, động vật hoang dã thuộc danh mục bảo tồn, thi thể, tro cốt.
- Tiền mặt, vàng miếng, kim loại quý, đá quý, tài liệu mật hoặc chứng từ có giá trị quy đổi thành tiền.
- Hàng lậu, hàng giả, hàng không có hóa đơn chứng từ hợp pháp.
Lưu ý: Nếu phát hiện hàng cấm, tài xế có quyền từ chối chở, chuyến xe bị hủy ngay và không được hoàn tiền.

4. MỨC ĐỘ BỒI THƯỜNG THIỆT HẠI HÀNG HÓA
- Hư hỏng toàn bộ hoặc mất mát do lỗi tài xế: Hoàn 100% cước phí chuyến đi và Đối tác Tài xế chịu trách nhiệm bồi thường thiệt hại thực tế theo giá trị thị trường của đồ đạc theo thỏa thuận giám định giữa hai bên.
- Hư hỏng, trầy xước một phần: Hỗ trợ chi phí sửa chữa, phục hồi hoặc sơn sửa theo chi phí thực tế phát sinh.
- Thời gian chi trả: Tiền bồi thường được Ban Quản trị chuyển khoản trực tiếp vào tài khoản ngân hàng chính chủ của khách hàng trong tối đa 03 ngày làm việc sau khi thống nhất mức đền bù.

5. CÁC TRƯỜNG HỢP MIỄN TRỪ TRÁCH NHIỆM
ModMove và Đối tác Tài xế được miễn trừ trách nhiệm bảo hành trong các trường hợp:
- Đồ dễ vỡ nhưng khách hàng tự đóng gói sơ sài, không chèn lót xốp chống sốc theo khuyến nghị.
- Hư hại do đặc tính tự nhiên của đồ đạc (cây cảnh gãy dập tự nhiên, thực phẩm tươi sống biến chất).
- Khách hàng tự ý bốc vác gây rơi vỡ ngoài phạm vi hỗ trợ của tài xế.
- Các sự kiện bất khả kháng theo quy định pháp luật (thiên tai, bão lũ, sạt lở đường hoặc lệnh phong tỏa cấm đường của cơ quan Nhà nước).'
WHERE `category` = 'POLICY' AND `code` = 'WARRANTY' AND (`description` IS NULL OR TRIM(`description`) = '');

-- 2. CHÍNH SÁCH HOÀN TIỀN (REFUND)
UPDATE `master_data_items`
SET `description` = '1. CÁC TRƯỜNG HỢP ĐƯỢC HOÀN TIỀN 100%
Khách hàng thanh toán trực tuyến qua Quét mã VietQR (PayOS) được hệ thống tự động hoàn tiền 100% trong các trường hợp:
- Khách hàng hủy chuyến sớm: Bấm hủy khi hệ thống đang tìm xe hoặc khi tài xế đang trên đường đến (trước khi tài xế bấm đã đến điểm lấy hàng).
- Không tìm được tài xế: Sau 10 phút kể từ lúc thanh toán mà không có tài xế nhận chuyến, hệ thống tự động hủy đơn và tạo lệnh hoàn tiền 100%.
- Sự cố phương tiện dọc đường không có xe thay thế: Xe hỏng giữa đường và bộ phận điều phối không tìm được xe khác thay thế -> Chuyến xe bị hủy, hoàn 100% cước phí và đồ đạc được đưa về điểm xuất phát miễn phí.
- Giao dịch thanh toán trễ: Khách hàng chuyển khoản sau khi mã QR đã hết hạn hoặc đơn hàng đã bị hủy.

2. CÁC TRƯỜNG HỢP KHÔNG ĐƯỢC HOÀN TIỀN
- Khách hàng vắng mặt tại điểm hẹn: Tài xế đã bấm đến nơi và chờ đợi từ 15 phút trở lên nhưng không liên lạc được với khách -> Chuyến xe bị hủy và không hoàn tiền cước nhằm hỗ trợ chi phí di chuyển cho tài xế.
- Vận chuyển hàng cấm: Khách hàng cố tình gửi hàng thuộc danh mục cấm vận chuyển.
- Khách hàng tự ý hủy chuyến khi tài xế đã đến nơi: Khi tài xế đã có mặt tại điểm bốc hàng, nếu khách hàng tự ý hủy chuyến mà không có lý do chính đáng, chuyến xe sẽ không được hoàn tiền.

3. THỜI HẠN VÀ CÁCH THỨC GỬI KHIẾU NẠI / HOÀN TIỀN
- Thời hạn gửi yêu cầu: Trong vòng 24 GIỜ kể từ thời điểm hàng được giao đến nơi (khi chuyến xe ở trạng thái Đã giao hàng). Sau 24 giờ hoặc sau khi khách đã bấm xác nhận nhận hàng, quyền khiếu nại trên hệ thống sẽ tự động đóng lại để chốt doanh thu cho tài xế.
- Cách gửi yêu cầu: Vào mục "Lịch sử chuyến đi" -> Chọn chuyến xe -> Bấm "Gửi khiếu nại / Yêu cầu hoàn tiền" -> Chọn lý do -> Nhập số tiền đề xuất hoàn trả và gửi kèm từ 01 đến 05 hình ảnh rõ nét chụp chi tiết hư hại làm bằng chứng.

4. PHƯƠNG THỨC VÀ THỜI GIAN CHI TRẢ HOÀN TIỀN
- Yêu cầu hoàn tiền do hủy chuyến tự động: Được chuyển thẳng tới Ban Quản trị phê duyệt ngay mà không qua bước trung gian.
- Khiếu nại dịch vụ / hư hỏng: Bộ phận hỗ trợ đối chiếu ảnh lúc bốc đồ lên xe và ảnh bàn giao để xác định mức bồi hoàn công tâm.
- Thời gian chi trả: Tiền hoàn được chuyển khoản trực tiếp vào tài khoản ngân hàng chính chủ của khách hàng trong vòng tối đa 03 NGÀY LÀM VIỆC (kèm mã giao dịch ngân hàng để đối soát minh bạch).'
WHERE `category` = 'POLICY' AND `code` = 'REFUND' AND (`description` IS NULL OR TRIM(`description`) = '');

-- 3. ĐIỀU KHOẢN SỬ DỤNG (TERMS_OF_USE)
UPDATE `master_data_items`
SET `description` = '1. BẢN CHẤT DỊCH VỤ VÀ CÁC BÊN THAM GIA
- ModMove là nền tảng công nghệ kết nối trung gian giữa người dùng có nhu cầu chuyển nhà, chở đồ và các Đối tác Tài xế độc lập có xe tải, xe van phù hợp. ModMove không phải là đơn vị kinh doanh vận tải trực tiếp và không sở hữu phương tiện. Hợp đồng vận chuyển được xác lập trực tiếp giữa Khách hàng và Đối tác Tài xế.

2. ĐĂNG KÝ VÀ QUẢN LÝ TÀI KHOẢN
- Mỗi số điện thoại và email chỉ đăng ký 01 tài khoản duy nhất. Email là tên đăng nhập chính và không thể thay đổi sau khi đăng ký. Mật khẩu phải từ 8 ký tự trở lên (gồm chữ và số).
- Tài khoản mới cần xác thực email bằng mã OTP gồm 6 chữ số (có hiệu lực trong 05 phút, nhập sai quá 5 lần sẽ bị hủy).
- Nhập sai mật khẩu liên tiếp 05 lần sẽ tạm khóa tài khoản trong 10 phút để phòng chống kẻ xấu dò mật khẩu.
- Điều kiện xóa tài khoản: Khách hàng không còn chuyến xe nào đang diễn ra; Đối tác Tài xế không còn lệnh rút tiền chờ duyệt và số dư ví đã về mức 0 đồng.

3. BẢNG PHÂN KHÚC PHƯƠNG TIỆN VÀ GIÁ CƯỚC TIÊU CHUẨN
- Xe van 500kg: Tải trọng 500kg, thùng 170x120x120 cm, giá mở cửa 50.000 đ, giá 10.000 đ/km, cước tối thiểu 80.000 đ.
- Xe tải 1 tấn: Tải trọng 1.000kg, thùng 300x160x160 cm, giá mở cửa 80.000 đ, giá 14.000 đ/km, cước tối thiểu 150.000 đ.
- Xe tải 1.5 tấn: Tải trọng 1.500kg, thùng 320x170x170 cm, giá mở cửa 100.000 đ, giá 17.000 đ/km, cước tối thiểu 200.000 đ.
- Xe tải 2 tấn: Tải trọng 2.000kg, thùng 400x180x180 cm, giá mở cửa 130.000 đ, giá 20.000 đ/km, cước tối thiểu 260.000 đ.
Tính năng lọt thùng thông minh: Ứng dụng tự động kiểm tra kích thước món đồ lớn nhất với lòng thùng xe (cho phép xoay chiều đồ đạc khi xếp).
Báo giá cố định minh bạch: Cước tính theo bản đồ Google Maps. Cam kết KHÔNG có phụ phí giờ cao điểm, mưa gió hay cồng kềnh. Phí dịch vụ bốc xếp tiêu chuẩn là 100.000 đ/chuyến nếu khách hàng chọn.

4. HÌNH THỨC THANH TOÁN
- Tiền mặt: Khách chọn Người gửi trả tại điểm lấy hoặc Người nhận trả tại điểm giao. Tài xế thu đúng số tiền hiển thị trên ứng dụng.
- Quét mã VietQR (PayOS): Mã QR thanh toán tự động có hiệu lực trong 10 phút. Ngay khi chuyển khoản thành công, hệ thống phát tín hiệu tìm tài xế ngay lập tức. Sau 30 phút chưa thanh toán, đơn hàng sẽ tự động hủy.

5. QUY TRÌNH 6 BƯỚC THỰC HIỆN CHUYẾN XE
- Bước 1: Tìm xe trong bán kính 5 km (tối đa 10 phút, quá 10 phút tự hủy và hoàn 100% tiền chuyển khoản).
- Bước 2: Tài xế đến điểm hẹn trong phạm vi 500 mét và cập nhật trạng thái.
- Bước 3: Bốc hàng lên xe, tài xế chụp từ 02 đến 05 bức ảnh hiện trạng gửi lên ứng dụng.
- Bước 4: Khách hàng kiểm tra ảnh trên ứng dụng và bấm "Đồng ý" duyệt ảnh thì xe mới được phép lăn bánh.
- Bước 5: Vận chuyển và giao hàng (khách theo dõi xe chạy realtime trên bản đồ; tài xế chụp ảnh xác nhận giao hàng).
- Bước 6: Hoàn tất chuyến đi (khách bấm xác nhận hoặc hệ thống tự động hoàn tất sau 24 giờ nếu không có khiếu nại).

6. QUY CHẾ VÍ TÀI XẾ VÀ KÝ QUỸ ĐƠN TIỀN MẶT
- Ví gồm Tài khoản Ký quỹ (duy trì tối thiểu 200.000 đ) và Tài khoản Thu nhập (tiền tích lũy từ các cuốc quét mã QR).
- Điều kiện nhận cuốc tiền mặt: Ký quỹ phải từ 200.000 đ trở lên VÀ lớn hơn hoặc bằng tiền phí dịch vụ của chuyến xe đó (10% tổng cước).
- Phí dịch vụ nền tảng: 10% tính trên cước thực thu. Chuyến chuyển khoản: Cộng 90% cước vào Tài khoản Thu nhập. Chuyến tiền mặt: Tài xế thu 100% tiền mặt, hệ thống tự động trừ 10% phí vào Tài khoản Ký quỹ.
- Cơ chế tự động bù cọc: Nếu sau chuyến tiền mặt mà ký quỹ dưới 200.000 đ nhưng Tài khoản Thu nhập có tiền, hệ thống tự động trích tiền từ thu nhập bù sang ký quỹ để tài xế tiếp tục nhận chuyến liên tục.
- Rút thu nhập: Không giới hạn mức tối thiểu (rút từ 1 đ trở lên), tiền về tài khoản ngân hàng chính chủ của tài xế trong tối đa 03 ngày làm việc.'
WHERE `category` = 'POLICY' AND `code` = 'TERMS_OF_USE' AND (`description` IS NULL OR TRIM(`description`) = '');

-- 4. CHÍNH SÁCH BẢO MẬT (PRIVACY)
UPDATE `master_data_items`
SET `description` = '1. MỤC ĐÍCH THU THẬP THÔNG TIN
ModMove chỉ thu thập các thông tin thực sự cần thiết nhằm phục vụ vận hành chuyến xe an toàn và minh bạch:
- Thông tin định danh: Họ tên, số điện thoại, địa chỉ email để tạo tài khoản, đăng nhập và liên hệ giao nhận hàng.
- Hình ảnh kiểm chứng: Ảnh chụp đồ đạc và ảnh xác nhận giao hàng được lưu trữ bảo mật trên máy chủ đám mây chuyên dụng, chỉ dùng cho mục đích giải quyết khiếu nại bồi thường.
- Thông tin ngân hàng: Số tài khoản, tên chủ tài khoản chỉ dùng duy nhất cho việc chi trả hoàn tiền bồi thường cho khách hoặc chuyển tiền thu nhập cho tài xế. ModMove tuyệt đối KHÔNG thu thập và KHÔNG lưu trữ mã PIN, mật khẩu ngân hàng hay mã OTP ngân hàng của bạn.

2. BẢO VỆ ĐỊNH VỊ GPS VÀ TỰ ĐỘNG XÓA SAU 30 NGÀY
- Định vị thời gian thực: Khi ứng dụng tài xế bật Online hoặc đang chở hàng, hệ thống cập nhật tọa độ GPS mỗi 05 giây một lần để hiển thị trực tiếp lộ trình xe chạy trên bản đồ cho khách hàng theo dõi và giám sát an toàn.
- TỰ ĐỘNG XÓA SẠCH NHẬT KÝ ĐỊNH VỊ SAU 30 NGÀY: Toàn bộ dữ liệu lịch sử vị trí GPS chi tiết của Đối tác Tài xế được hệ thống tự động quét và XÓA SẠCH VĨNH VIỄN KHỎI MÁY CHỦ SAU 30 NGÀY LƯU TRỮ nhằm đảm bảo tuyệt đối quyền riêng tư di chuyển.

3. AN TOÀN BẢO MẬT HỆ THỐNG
- Toàn bộ dữ liệu truyền tải giữa ứng dụng/website và máy chủ đều được mã hóa bằng giao thức HTTPS/TLS tiêu chuẩn cao.
- Mật khẩu tài khoản được mã hóa một chiều an toàn; hệ thống phân quyền nghiêm ngặt đảm bảo người dùng chỉ xem và thao tác trên chuyến xe của chính mình.
- Mã xác thực email tự hết hạn sau 05 phút; mã QR thanh toán chưa chuyển khoản tự hủy sau 10 phút.

4. QUYỀN CỦA BẠN ĐỐI VỚI DỮ LIỆU CÁ NHÂN
- Bạn có quyền tra cứu, cập nhật thông tin cá nhân và hình đại diện trên ứng dụng bất kỳ lúc nào.
- Bạn có quyền tra cứu lại toàn bộ lịch sử chuyến xe và lịch sử giao dịch ví của mình.
- Bạn có quyền yêu cầu xóa tài khoản và ngưng sử dụng dịch vụ khi không còn ràng buộc chuyến đi chưa xong hoặc công nợ tài chính.'
WHERE `category` = 'POLICY' AND `code` = 'PRIVACY' AND (`description` IS NULL OR TRIM(`description`) = '');
