import { queryOptions, useQuery, type QueryClient } from '@tanstack/react-query';
import axios from 'axios';

import {
  fetchChatMessages,
  fetchChatOverview,
  fetchConsents,
  fetchDraftOrders,
  fetchDueReminders,
  fetchFamilyMembers,
  fetchMedicationAdherenceSummary,
  fetchOrdersOverview,
  fetchPurchases,
  fetchReminders,
  fetchRemindersOverview,
  fetchRepurchaseSuggestions,
  fetchReservations,
  markChatRead,
  type ChatOverview,
  type RemindersOverview,
} from '@/shared/api/customer-app.api';
import { CUSTOMER_APP_CHAT_CONSENT } from '@/shared/api/customer-app.types';
import { OVERVIEW_CACHE_KEYS, readOverviewCache, writeOverviewCache } from '@/shared/api/overview-cache';
import { useAuthStore } from '@/shared/auth/auth.store';

/** Stale-while-revalidate window for tab overview payloads. */
export const OVERVIEW_STALE_MS = 2 * 60 * 1000;
export const OVERVIEW_GC_MS = 5 * 60 * 1000;

function accountScope(): string {
  const profile = useAuthStore.getState().profile;
  if (!profile) return 'anon';
  return `${profile.tenantCode}:${profile.customerId}`;
}

export const overviewQueryKeys = {
  all: ['customer-overview'] as const,
  scope: () => [...overviewQueryKeys.all, accountScope()] as const,
  orders: () => [...overviewQueryKeys.scope(), 'orders'] as const,
  reminders: () => [...overviewQueryKeys.scope(), 'reminders'] as const,
  chat: () => [...overviewQueryKeys.scope(), 'chat'] as const,
};

export type OrdersOverview = Awaited<ReturnType<typeof fetchOrdersOverview>>;

async function resolveOrdersOverview(): Promise<OrdersOverview> {
  try {
    const data = await fetchOrdersOverview();
    writeOverviewCache(OVERVIEW_CACHE_KEYS.orders, data);
    return data;
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 404) {
      throw error;
    }

    const [draftResult, purchaseResult, reservationResult] = await Promise.allSettled([
      fetchDraftOrders(),
      fetchPurchases(),
      fetchReservations(),
    ]);

    if (draftResult.status !== 'fulfilled') {
      throw draftResult.reason;
    }

    const data: OrdersOverview = {
      draftOrders: draftResult.value,
      purchases: purchaseResult.status === 'fulfilled' ? purchaseResult.value : [],
      reservations: reservationResult.status === 'fulfilled' ? reservationResult.value : [],
      hasReservationsModule: reservationResult.status === 'fulfilled',
    };
    writeOverviewCache(OVERVIEW_CACHE_KEYS.orders, data);
    return data;
  }
}

async function resolveRemindersOverview(): Promise<RemindersOverview> {
  try {
    const data = await fetchRemindersOverview();
    writeOverviewCache(OVERVIEW_CACHE_KEYS.reminders, data);
    return data;
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 404) {
      throw error;
    }

    const [remindersResult, summaryResult, dueResult, repurchaseResult, familyResult] =
      await Promise.allSettled([
        fetchReminders(true),
        fetchMedicationAdherenceSummary(),
        fetchDueReminders(),
        fetchRepurchaseSuggestions(),
        fetchFamilyMembers(),
      ]);

    if (remindersResult.status !== 'fulfilled') {
      throw remindersResult.reason;
    }

    const data: RemindersOverview = {
      reminders: remindersResult.value.items,
      adherence:
        summaryResult.status === 'fulfilled'
          ? summaryResult.value
          : {
              dueCount: 0,
              takenToday: 0,
              skippedToday: 0,
              scheduledToday: 0,
              missedStreakDays: 0,
              showMissedAlert: false,
            },
      dueReminders: dueResult.status === 'fulfilled' ? dueResult.value : [],
      repurchaseSuggestions: repurchaseResult.status === 'fulfilled' ? repurchaseResult.value : [],
      familyMembers: familyResult.status === 'fulfilled' ? familyResult.value : [],
    };
    writeOverviewCache(OVERVIEW_CACHE_KEYS.reminders, data);
    return data;
  }
}

async function resolveChatOverview(): Promise<ChatOverview> {
  try {
    const data = await fetchChatOverview();
    writeOverviewCache(OVERVIEW_CACHE_KEYS.chat, data);
    return data;
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 404) {
      throw error;
    }

    const [consents, page] = await Promise.all([fetchConsents(), fetchChatMessages()]);
    const chatConsent = consents.find(
      (c) =>
        c.channel === CUSTOMER_APP_CHAT_CONSENT.channel &&
        c.purpose === CUSTOMER_APP_CHAT_CONSENT.purpose,
    );
    await markChatRead().catch(() => undefined);

    const data: ChatOverview = {
      consents,
      messages: page.items,
      hasMore: page.hasMore,
      thread: {
        threadId: '',
        unreadCount: 0,
        lastMessageAt: page.items.at(-1)?.createdAt ?? null,
        lastMessagePreview: page.items.at(-1)?.body ?? null,
      },
      chatConsentGranted: chatConsent?.granted ?? false,
    };
    writeOverviewCache(OVERVIEW_CACHE_KEYS.chat, data);
    return data;
  }
}

export function ordersOverviewQueryOptions() {
  return queryOptions({
    queryKey: overviewQueryKeys.orders(),
    queryFn: resolveOrdersOverview,
    staleTime: OVERVIEW_STALE_MS,
    gcTime: OVERVIEW_GC_MS,
    placeholderData: () => readOverviewCache<OrdersOverview>(OVERVIEW_CACHE_KEYS.orders) ?? undefined,
  });
}

export function remindersOverviewQueryOptions() {
  return queryOptions({
    queryKey: overviewQueryKeys.reminders(),
    queryFn: resolveRemindersOverview,
    staleTime: OVERVIEW_STALE_MS,
    gcTime: OVERVIEW_GC_MS,
    placeholderData: () => readOverviewCache<RemindersOverview>(OVERVIEW_CACHE_KEYS.reminders) ?? undefined,
  });
}

export function chatOverviewQueryOptions() {
  return queryOptions({
    queryKey: overviewQueryKeys.chat(),
    queryFn: resolveChatOverview,
    staleTime: OVERVIEW_STALE_MS,
    gcTime: OVERVIEW_GC_MS,
    placeholderData: () => readOverviewCache<ChatOverview>(OVERVIEW_CACHE_KEYS.chat) ?? undefined,
  });
}

function useIsCustomerAuthed() {
  return useAuthStore((s) => Boolean(s.accessToken));
}

export function useOrdersOverviewQuery() {
  const enabled = useIsCustomerAuthed();
  return useQuery({ ...ordersOverviewQueryOptions(), enabled });
}

export function useRemindersOverviewQuery() {
  const enabled = useIsCustomerAuthed();
  return useQuery({ ...remindersOverviewQueryOptions(), enabled });
}

export function useChatOverviewQuery() {
  const enabled = useIsCustomerAuthed();
  return useQuery({ ...chatOverviewQueryOptions(), enabled });
}

export function prefetchOrdersOverview(client: QueryClient) {
  if (!useAuthStore.getState().accessToken) return Promise.resolve();
  return client.prefetchQuery(ordersOverviewQueryOptions());
}

export function prefetchRemindersOverview(client: QueryClient) {
  if (!useAuthStore.getState().accessToken) return Promise.resolve();
  return client.prefetchQuery(remindersOverviewQueryOptions());
}

export function prefetchChatOverview(client: QueryClient) {
  if (!useAuthStore.getState().accessToken) return Promise.resolve();
  return client.prefetchQuery(chatOverviewQueryOptions());
}

/** Warm orders + reminders while the user is on Home. */
export function prefetchPrimaryTabOverviews(client: QueryClient) {
  return Promise.allSettled([prefetchOrdersOverview(client), prefetchRemindersOverview(client)]);
}

export function prefetchOverviewForPath(client: QueryClient, path: string) {
  if (path === '/orders') return prefetchOrdersOverview(client);
  if (path === '/reminders') return prefetchRemindersOverview(client);
  if (path === '/chat') return prefetchChatOverview(client);
  return Promise.resolve();
}

export function resetCustomerOverviewQueries(client: QueryClient) {
  client.removeQueries({ queryKey: overviewQueryKeys.all });
}
