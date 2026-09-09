import { type ReactNode } from 'react';

/** Character look cards and the Director workspace live in children. Production state is the workspace, not a second banner stack. */
export function ContentFamixaLookBoard({ children }: { children: ReactNode }) {
  return <section className="fx-look">{children}</section>;
}
