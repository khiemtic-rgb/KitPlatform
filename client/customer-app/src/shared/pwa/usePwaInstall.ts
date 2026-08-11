import { useCallback, useEffect, useMemo, useState } from 'react';

const VISIT_KEY = 'novixa.customerPwa.visitCount';
const VISIT_SESSION_KEY = 'novixa.customerPwa.visitBumped';
const DISMISS_KEY = 'novixa.customerPwa.installDismissedAt';
const DISMISS_MS = 14 * 24 * 60 * 60 * 1000;

export type BeforeInstallPromptEventLike = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
};

function readVisitCount(): number {
  try {
    return Math.max(0, Number(localStorage.getItem(VISIT_KEY) ?? '0') || 0);
  } catch {
    return 0;
  }
}

function bumpVisitCountOncePerSession(): number {
  const current = readVisitCount();
  try {
    if (sessionStorage.getItem(VISIT_SESSION_KEY) === '1') return current;
    sessionStorage.setItem(VISIT_SESSION_KEY, '1');
    const next = current + 1;
    localStorage.setItem(VISIT_KEY, String(next));
    return next;
  } catch {
    return current;
  }
}

function isDismissedRecently(): boolean {
  try {
    const raw = localStorage.getItem(DISMISS_KEY);
    if (!raw) return false;
    const at = Number(raw);
    if (!Number.isFinite(at)) return false;
    return Date.now() - at < DISMISS_MS;
  } catch {
    return false;
  }
}

export function isPwaStandalone(): boolean {
  if (typeof window === 'undefined') return false;
  const mq = window.matchMedia?.('(display-mode: standalone)')?.matches;
  const iosStandalone = (navigator as Navigator & { standalone?: boolean }).standalone === true;
  return Boolean(mq || iosStandalone);
}

export function isIosSafari(): boolean {
  if (typeof navigator === 'undefined') return false;
  const ua = navigator.userAgent;
  const iOS =
    /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  const webkit = /WebKit/.test(ua);
  const chromeIos = /CriOS|FxiOS|EdgiOS/.test(ua);
  return iOS && webkit && !chromeIos;
}

/** Hook cài PWA: Chrome/Android prompt + hướng dẫn iOS. */
export function usePwaInstall() {
  const [deferred, setDeferred] = useState<BeforeInstallPromptEventLike | null>(null);
  const [installed, setInstalled] = useState(() => isPwaStandalone());
  const [visitCount, setVisitCount] = useState(() => readVisitCount());
  const [dismissed, setDismissed] = useState(() => isDismissedRecently());

  useEffect(() => {
    setVisitCount(bumpVisitCountOncePerSession());
    setInstalled(isPwaStandalone());

    const onBip = (event: Event) => {
      event.preventDefault();
      setDeferred(event as BeforeInstallPromptEventLike);
    };
    const onInstalled = () => {
      setInstalled(true);
      setDeferred(null);
    };
    window.addEventListener('beforeinstallprompt', onBip);
    window.addEventListener('appinstalled', onInstalled);
    return () => {
      window.removeEventListener('beforeinstallprompt', onBip);
      window.removeEventListener('appinstalled', onInstalled);
    };
  }, []);

  const canNativeInstall = Boolean(deferred) && !installed;
  const showIosGuide = isIosSafari() && !installed;
  const canOfferInstall = !installed && (canNativeInstall || showIosGuide);

  const softBannerEligible = useMemo(() => {
    if (!canOfferInstall || dismissed) return false;
    return visitCount >= 2;
  }, [canOfferInstall, dismissed, visitCount]);

  const install = useCallback(async () => {
    if (!deferred) return false;
    await deferred.prompt();
    const choice = await deferred.userChoice;
    setDeferred(null);
    if (choice.outcome === 'accepted') {
      setInstalled(true);
      return true;
    }
    return false;
  }, [deferred]);

  const dismiss = useCallback(() => {
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now()));
    } catch {
      /* ignore */
    }
    setDismissed(true);
  }, []);

  return {
    installed,
    canNativeInstall,
    showIosGuide,
    canOfferInstall,
    softBannerEligible,
    install,
    dismiss,
  };
}
