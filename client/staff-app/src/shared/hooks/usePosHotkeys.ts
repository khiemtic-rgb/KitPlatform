import { useEffect } from 'react';

export type PosHotkeyHandlers = {
  onFocusSearch?: () => void;
  onCheckout?: () => void;
  onSaveDraft?: () => void;
  onComplete?: () => void;
  onPrint?: () => void;
  onNewOrder?: () => void;
  onSaveNote?: () => void;
  onEscape?: () => void;
};

function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
  return target.isContentEditable;
}

/** POS hotkeys — F-keys work globally; letter keys when not typing in inputs. */
export function usePosHotkeys(handlers: PosHotkeyHandlers, enabled = true) {
  useEffect(() => {
    if (!enabled) return;

    const onKeyDown = (e: KeyboardEvent) => {
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
        case 'F12':
          e.preventDefault();
          handlers.onComplete?.();
          return;
        case 'Escape':
          handlers.onEscape?.();
          return;
        default:
          break;
      }

      if (typing || e.ctrlKey || e.metaKey || e.altKey) return;

      const key = e.key.toLowerCase();
      if (key === 'p') {
        e.preventDefault();
        handlers.onPrint?.();
      } else if (key === 'n') {
        e.preventDefault();
        handlers.onNewOrder?.();
      } else if (key === 's' && e.shiftKey) {
        e.preventDefault();
        handlers.onSaveNote?.();
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled, handlers]);
}
