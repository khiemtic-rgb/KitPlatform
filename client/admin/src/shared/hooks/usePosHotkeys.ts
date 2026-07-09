import { useEffect } from 'react';

export type AdminPosHotkeyHandlers = {
  onFocusSearch?: () => void;
  onCheckout?: () => void;
  onSaveDraft?: () => void;
  onPrint?: () => void;
  onEscape?: () => void;
};

function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
  return target.isContentEditable;
}

export function usePosHotkeys(handlers: AdminPosHotkeyHandlers, enabled = true) {
  useEffect(() => {
    if (!enabled) return;

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key.toLowerCase() === 'p') {
        e.preventDefault();
        handlers.onPrint?.();
        return;
      }

      const typing = isTypingTarget(e.target);

      switch (e.key) {
        case 'F2':
          e.preventDefault();
          handlers.onFocusSearch?.();
          return;
        case 'F8':
          e.preventDefault();
          handlers.onCheckout?.();
          return;
        case 'F9':
          e.preventDefault();
          handlers.onSaveDraft?.();
          return;
        case 'Escape':
          handlers.onEscape?.();
          return;
        default:
          break;
      }

      if (typing || e.ctrlKey || e.metaKey || e.altKey) return;
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled, handlers]);
}
