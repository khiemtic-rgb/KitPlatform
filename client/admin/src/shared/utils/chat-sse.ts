import { apiPath } from '@/shared/api/api-base';
import {
  ADMIN_CHAT_EVENTS_PATH,
  ADMIN_DRAFT_ORDER_EVENTS_PATH,
  subscribeAuthorizedSse,
} from '@/shared/utils/bearer-transport';

export function subscribeChatSse(
  url: string | null,
  accessToken: string | null,
  onEvent: () => void,
): () => void {
  return subscribeAuthorizedSse(url, accessToken, onEvent);
}

export function buildAdminChatEventsUrl() {
  return apiPath(ADMIN_CHAT_EVENTS_PATH);
}

export function buildAdminDraftOrderEventsUrl() {
  return apiPath(ADMIN_DRAFT_ORDER_EVENTS_PATH);
}
