import type { Metadata } from "next";
import "./globals.css";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";

export const metadata: Metadata = {
  title: "Orphac Athera — Enterprise Resource Planning",
  description: "Industry-neutral multi-tenant ERP platform for Indonesian SMEs.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="id">
      <body>
        <LocaleProvider>{children}</LocaleProvider>
      </body>
    </html>
  );
}
