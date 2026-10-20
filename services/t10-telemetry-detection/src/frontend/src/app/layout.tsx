import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "SCUTUM · T10",
  description: "Log izvori, telemetry ingestion i detection tuning",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="sr" className="h-full antialiased">
      <body className="flex min-h-full flex-col">{children}</body>
    </html>
  );
}
