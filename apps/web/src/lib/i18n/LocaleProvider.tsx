"use client";

import React, { createContext, useCallback, useContext, useEffect, useSyncExternalStore } from "react";
import { Dictionary, MESSAGES, SupportedLocale } from "./messages";

interface LocaleContextValue {
  locale: SupportedLocale;
  setLocale: (next: SupportedLocale) => void;
  t: Dictionary;
}

const LocaleContext = createContext<LocaleContextValue | null>(null);
const STORAGE_KEY = "athera.locale";
const listeners = new Set<() => void>();

function subscribe(callback: () => void): () => void {
  listeners.add(callback);
  const onStorage = (event: StorageEvent) => {
    if (event.key === STORAGE_KEY) {
      callback();
    }
  };
  window.addEventListener("storage", onStorage);
  return () => {
    listeners.delete(callback);
    window.removeEventListener("storage", onStorage);
  };
}

function getClientSnapshot(): SupportedLocale {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    if (saved === "id-ID" || saved === "en-US") {
      return saved;
    }
  } catch {
    // Storage unavailable
  }
  return "id-ID";
}

function getServerSnapshot(): SupportedLocale {
  return "id-ID";
}

export function hasSavedLocalePreference(): boolean {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    return saved === "id-ID" || saved === "en-US";
  } catch {
    return false;
  }
}

export function LocaleProvider({ children }: { children: React.ReactNode }) {
  const locale = useSyncExternalStore(subscribe, getClientSnapshot, getServerSnapshot);

  useEffect(() => {
    document.documentElement.lang = locale === "id-ID" ? "id" : "en";
  }, [locale]);

  const setLocale = useCallback((next: SupportedLocale) => {
    try {
      window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Ignore storage failures cleanly
    }
    listeners.forEach((listener) => listener());
  }, []);

  return (
    <LocaleContext.Provider value={{ locale, setLocale, t: MESSAGES[locale] }}>
      {children}
    </LocaleContext.Provider>
  );
}

export function useLocale(): LocaleContextValue {
  const ctx = useContext(LocaleContext);
  if (!ctx) {
    throw new Error("useLocale must be used inside LocaleProvider");
  }
  return ctx;
}
