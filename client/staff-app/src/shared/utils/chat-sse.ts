import { apiPath } from '@/shared/api/api-base';
import { STAFF_CHAT_EVENTS_PATH, subscribeAuthorizedSse } from '@/shared/utils/bearer-transport';

export function subscribeChatSse(
  url: string | null,
  accessToken: string | null,
  onEvent: () => void,
): () => void {
  return subscribeAuthorizedSse(url, accessToken, onEvent);
}

export function buildChatEventsUrl() {
  return apiPath(STAFF_CHAT_EVENTS_PATH);
}
