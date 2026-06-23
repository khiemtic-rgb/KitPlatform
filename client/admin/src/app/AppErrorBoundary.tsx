import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button, Result } from 'antd';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('PharmaCore render error:', error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div
          style={{
            minHeight: '100vh',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: '#f5f5f5',
            padding: 24,
          }}
        >
          <Result
            status="error"
            title="Không tải được giao diện"
            subTitle={this.state.error.message || 'Có lỗi JavaScript khi khởi động ứng dụng.'}
            extra={
              <Button type="primary" onClick={() => window.location.reload()}>
                Tải lại trang
              </Button>
            }
          />
        </div>
      );
    }

    return this.props.children;
  }
}
