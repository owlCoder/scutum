import { AppHeader } from "@/components/layout/app-header";

// Zaštićeni deo aplikacije. Provera sesije na serveru dolazi sa autentikacijom (T10-020);
// do tada je ovo samo zajednički raspored stranica.
export default function AppLayout({ children }: LayoutProps<"/">) {
  return (
    <>
      <AppHeader />
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">{children}</main>
    </>
  );
}
