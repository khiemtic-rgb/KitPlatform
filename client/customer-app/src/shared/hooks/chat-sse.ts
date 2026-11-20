import { apiPath } from '@/shared/api/api-base';
import {
  CUSTOMER_CHAT_EVENTS_PATH,
  CUSTOMER_DRAFT_ORDER_EVENTS_PATH,
  subscribeAuthorizedSse,
} from '@/shared/utils/bearer-transport';

export function subscribeChatSse(
  url: string | null,
  accessToken: string | null,
  onEvent: () => void,
): () => void {
  return subscribeAuthorizedSse(url, accessToken, onEvent);
}

export function buildCustomerChatEventsUrl() {
  return apiPath(CUSTOMER_CHAT_EVENTS_PATH);
}

export function buildCustomerDraftOrderEventsUrl() {
  return apiPath(CUSTOMER_DRAFT_ORDER_EVENTS_PATH);
}
