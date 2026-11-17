import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import LoginPage from "@/app/login/page";
import { LocaleProvider } from "@/lib/i18n/LocaleProvider";
import { formatIdr, formatJakartaDateTime } from "@/lib/i18n/messages";

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    replace: vi.fn(),
    refresh: vi.fn(),
    push: vi.fn(),
  }),
}));

vi.mock("@/lib/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/client")>("@/lib/api/client");
  return {
    ...actual,
    ensureCsrfToken: vi.fn().mockResolvedValue("mock-csrf-token"),
    loginWithPassword: vi.fn(),
  };
});

describe("S00 Formatting and Login UI", () => {
  it("formats IDR amounts and Jakarta timestamps deterministically", () => {
    const idr = formatIdr(2750000);
    expect(idr).toContain("2.750.000");

    expect(formatJakartaDateTime(null, "id-ID")).toBe("—");
    const ts = formatJakartaDateTime("2026-11-18T00:00:00Z", "id-ID");
    expect(ts).not.toBe("—");
  });

  it("renders Indonesian default login copy, switches to English, and validates empty submit", () => {
    render(
      <LocaleProvider>
        <LoginPage />
      </LocaleProvider>,
    );

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Masuk ke Sistem Operasional");
    expect(screen.getByRole("button", { name: "Masuk ke Athera" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "English" }));
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Sign In to Operations");

    fireEvent.click(screen.getByRole("button", { name: "Sign in to Athera" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Enter both email and password before signing in.");
  });
});
