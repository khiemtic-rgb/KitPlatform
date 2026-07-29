'use client';

import { Moon, Sun } from 'lucide-react';
import { useTheme } from '@/components/providers/ThemeProvider';
import { Button } from '@/components/ui/Button';

export function ThemeToggle({ className = '' }: { className?: string }) {
  const { resolved, toggle } = useTheme();
  const isDark = resolved === 'dark';
  return (
    <Button
      variant="outline"
      className={`min-h-10 px-3 ${className}`.trim()}
      onClick={toggle}
      aria-label={isDark ? 'Chuyển sáng' : 'Chuyển tối'}
    >
      {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
    </Button>
  );
}
