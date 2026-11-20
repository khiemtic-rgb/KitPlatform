export const SSE_RETRY_START_MS = 1000;
export const SSE_RETRY_MAX_MS = 30_000;

export const ADMIN_CHAT_EVENTS_PATH = '/api/sales/customer-chat/events';
export const ADMIN_DRAFT_ORDER_EVENTS_PATH = '/api/sales/customer-draft-orders/events';

export function nextSseRetryMs(current: number): number {
  return Math.min(current * 2, SSE_RETRY_MAX_MS);
}

export function stripAccessToken(url: string): string {
  const hashIndex = url.indexOf('#');
  const hash = hashIndex >= 0 ? url.slice(hashIndex) : '';
  const withoutHash = hashIndex >= 0 ? url.slice(0, hashIndex) : url;
  const queryIndex = withoutHash.indexOf('?');
  if (queryIndex < 0) return url;
  const path = withoutHash.slice(0, queryIndex);
  const params = new URLSearchParams(withoutHash.slice(queryIndex + 1));
  params.delete('access_token');
  const rest = params.toString();
  return `${path}${rest ? `?${rest}` : ''}${hash}`;
}

export function authorizedSseRequest(url: string, accessToken: string): {
  url: string;
  headers: { Authorization: string; Accept: string };
  credentials: 'same-origin';
} {
  const clean = stripAccessToken(url);
  return {
    url: clean,
    headers: {
      Authorization: `Bearer ${accessToken}`,
      Accept: 'text/event-stream',
    },
    credentials: 'same-origin',
  };
}

/** Dispatch unnamed `data:` blocks. Comment keepalives and named events stay quiet. */
export function consumeSseBuffer(buffer: string, onData: (data: string) => void): string {
  const parts = buffer.split('\n\n');
  const rest = parts.pop() ?? '';
  for (const block of parts) {
    const dataLines: string[] = [];
    let named = false;
    for (const line of block.split('\n')) {
      if (!line || line.startsWith(':')) continue;
      if (line.startsWith('event:')) {
        const name = line.slice(6).trim();
        if (name && name !== 'message') named = true;
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).replace(/^ /, ''));
      }
    }
    if (!named && dataLines.length > 0) onData(dataLines.join('\n'));
  }
  return rest;
}

export type SseRuntime = {
  fetch: (url: string, init: RequestInit) => Promise<Response>;
  setTimeout: (fn: () => void, ms: number) => number;
  clearTimeout: (id: number) => void;
};

export function browserSseRuntime(): SseRuntime {
  return {
    fetch: (url, init) => fetch(url, init),
    setTimeout: (fn, ms) => window.setTimeout(fn, ms),
    clearTimeout: (id) => window.clearTimeout(id),
  };
}

export function subscribeAuthorizedSse(
  url: string | null,
  accessToken: string | null,
  onEvent: () => void,
  runtime: SseRuntime = browserSseRuntime(),
): () => void {
  if (!url || !accessToken) return () => undefined;

  let closed = false;
  let retryMs = SSE_RETRY_START_MS;
  let retryTimer: number | undefined;
  let abort: AbortController | null = null;

  const connect = () => {
    if (closed) return;
    abort?.abort();
    abort = new AbortController();
    const signal = abort.signal;
    const request = authorizedSseRequest(url, accessToken);

    void (async () => {
      try {
        const response = await runtime.fetch(request.url, {
          method: 'GET',
          headers: request.headers,
          credentials: request.credentials,
          signal,
        });
        if (!response.ok || !response.body) throw new Error('sse');
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        while (!signal.aborted) {
          const { done, value } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });
          buffer = consumeSseBuffer(buffer, () => {
            retryMs = SSE_RETRY_START_MS;
            onEvent();
          });
        }
      } catch {
        if (signal.aborted || closed) return;
      }
      if (closed || signal.aborted) return;
      const delay = retryMs;
      retryMs = nextSseRetryMs(retryMs);
      retryTimer = runtime.setTimeout(connect, delay);
    })();
  };

  connect();

  return () => {
    closed = true;
    if (retryTimer !== undefined) runtime.clearTimeout(retryTimer);
    abort?.abort();
  };
}

export function adminResourceRequest(absoluteUrl: string, token: string | null): {
  url: string;
  authenticated: boolean;
  headers?: { Authorization: string };
} {
  const url = stripAccessToken(absoluteUrl);
  const authenticated = url.includes('/uploads/products/') || url.includes('/uploads/health-records/');
  if (!authenticated || !token) return { url, authenticated: false };
  return { url, authenticated: true, headers: { Authorization: `Bearer ${token}` } };
}
