import { Card, Empty, List, Typography } from 'antd';
import type { CustomerPosFrequentProductDto } from '@/shared/api/pos-customer-context.types';

const { Text } = Typography;

export function FrequentPurchasesList({ products }: { products: CustomerPosFrequentProductDto[] }) {
  return (
    <Card size="small" title="SẢN PHẨM HAY CÓ TRONG ĐƠN MUA">
      <Text type="secondary" style={{ fontSize: 11, fontStyle: 'italic' }}>
        Chỉ là tần suất mua. Không suy ra bệnh và không coi là khách đã sử dụng.
      </Text>
      {products.length === 0 ? (
        <Empty description="Chưa có sản phẩm mua từ hai lần trở lên" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      ) : (
        <List
          size="small"
          dataSource={products}
          renderItem={(item) => (
            <List.Item>
              <Text>{item.productName}</Text>
              <Text type="secondary">{item.orderCount} lần mua</Text>
            </List.Item>
          )}
        />
      )}
    </Card>
  );
}
