import { apiPath } from '@/shared/api/api-base';

export function subscribeChatSse(url: string | null, onEvent: () => void): () => void {
  if (!url) return () => undefined;

  let es: EventSource | null = null;
  let closed = false;
  let retryMs = 1000;
  let retryTimer: number | undefined;

  const connect = () => {
    if (closed) return;
    es?.close();
    es = new EventSource(url);
    es.onmessage = () => {
      retryMs = 1000;
      onEvent();
    };
    es.onerror = () => {
      es?.close();
      es = null;
      if (closed) return;
      retryTimer = window.setTimeout(connect, retryMs);
      retryMs = Math.min(retryMs * 2, 30_000);
    };
  };

  connect();

  return () => {
    closed = true;
    if (retryTimer) window.clearTimeout(retryTimer);
    es?.close();
  };
}

export function buildCustomerChatEventsUrl(accessToken: string) {
  return `${apiPath('/api/customer-app/chat/events')}?access_token=${encodeURIComponent(accessToken)}`;
}

export function buildCustomerDraftOrderEventsUrl(accessToken: string) {
  return `${apiPath('/api/customer-app/draft-orders/events')}?access_token=${encodeURIComponent(accessToken)}`;
}
