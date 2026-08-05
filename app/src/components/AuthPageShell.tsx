import type { ReactNode } from "react";

interface AuthPageShellProps {
  title: string;
  description: string;
  footer: ReactNode;
  children: ReactNode;
}

/** Provides a consistent, non-marketing surface for login and registration forms. */
export default function AuthPageShell({
  title,
  description,
  footer,
  children,
}: AuthPageShellProps) {
  return (
    <section className="mx-auto flex w-full max-w-md flex-col justify-center py-4 sm:py-10">
      <div className="rounded-xl border border-base-300 bg-base-100/90 p-6 shadow-sm backdrop-blur sm:p-8">
        <header className="mb-7 space-y-2">
          <h1 className="text-2xl font-bold">{title}</h1>
          <p className="text-sm leading-6 text-base-content/70">
            {description}
          </p>
        </header>
        {children}
        <div className="mt-6 border-t border-base-300 pt-5 text-center text-sm text-base-content/70">
          {footer}
        </div>
      </div>
    </section>
  );
}
