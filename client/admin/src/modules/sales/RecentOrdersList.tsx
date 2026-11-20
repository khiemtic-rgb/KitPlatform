import { Card, Empty, List, Typography } from 'antd';
import type { CustomerPosOrderDto } from '@/shared/api/pos-customer-context.types';
import { formatDisplayMoney } from '@/shared/utils/money';

const { Text } = Typography;

interface RecentOrdersListProps {
  orders: CustomerPosOrderDto[];
}

export function RecentOrdersList({ orders }: RecentOrdersListProps) {
  if (orders.length === 0) {
    return (
      <Card size="small" title="LỊCH SỬ MUA GẦN ĐÂY" className="pos-recent-orders-card">
        <Empty description="Chưa có giao dịch mua trong 90 ngày" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      </Card>
    );
  }

  return (
    <Card size="small" title="LỊCH SỬ MUA GẦN ĐÂY" className="pos-recent-orders-card">
      <Text type="secondary" style={{ fontSize: 11, fontStyle: 'italic' }}>
        Giao dịch đã mua. Không suy ra khách đã sử dụng các sản phẩm này.
      </Text>
      <List
        size="small"
        dataSource={orders}
        renderItem={(order) => (
          <List.Item>
            <div style={{ width: '100%' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <Text strong>
                  {new Date(order.orderDate).toLocaleDateString('vi-VN')}
                </Text>
                <Text strong>{formatDisplayMoney(order.totalAmount)}</Text>
              </div>
              {order.topProducts.length > 0 && (
                <div style={{ paddingLeft: 8 }}>
                  {order.topProducts.map((product, idx) => (
                    <div key={idx}>
                      <Text type="secondary" style={{ fontSize: 12 }}>
                        • {product.productName} ({product.quantity})
                      </Text>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </List.Item>
        )}
      />
    </Card>
  );
}
