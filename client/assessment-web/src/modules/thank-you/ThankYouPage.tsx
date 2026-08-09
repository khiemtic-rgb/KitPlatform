import { Button, Typography } from 'antd';

const { Title, Paragraph } = Typography;

const NOVIXA_HOME = 'https://novixa.vn/vi/';

export function ThankYouPage() {
  return (
    <div className="page-shell" style={{ textAlign: 'center', paddingTop: '3rem' }}>
      <Title level={3}>Cảm ơn bạn!</Title>
      <Paragraph>
        Novixa sẽ liên hệ nếu bạn đã gửi thông tin. Bạn có thể làm lại khảo sát bất cứ lúc nào.
      </Paragraph>
      <Button type="primary" href={NOVIXA_HOME}>
        Về trang chủ Novixa
      </Button>
    </div>
  );
}
