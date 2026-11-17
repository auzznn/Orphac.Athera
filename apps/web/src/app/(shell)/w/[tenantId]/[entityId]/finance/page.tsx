"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowLeft,
  BookOpen,
  CalendarClock,
  CheckCircle2,
  Clock,
  CornerDownRight,
  FileSpreadsheet,
  Info,
  Landmark,
  Lock,
  Plus,
  Receipt,
  RotateCcw,
  Trash2,
} from "lucide-react";
import { ScopedBoundaryError, ScopedLoadingSheet } from "@/components/ScopedBoundaryState";
import {
  AccountMappingListResponse,
  ApiProblemError,
  CloseFinancialPeriodRequest,
  closeFinancialPeriod,
  createFinancialPeriod,
  createGlAccount,
  createTaxCode,
  EntityWorkspaceDetailResponse,
  EvaluateTaxPreviewResponse,
  evaluateTaxPreview,
  fetchAccountMappings,
  fetchEntityWorkspaceDetail,
  fetchFinancialPeriods,
  fetchGlAccounts,
  fetchJournalDetail,
  fetchJournals,
  fetchTaxCodes,
  FinancialPeriodListResponse,
  GlAccountDto,
  GlAccountListResponse,
  JournalDetailDto,
  PagedJournalsResponse,
  postJournal,
  reverseJournal,
  TaxCodeListResponse,
  updateGlAccount,
  upsertAccountMapping,
} from "@/lib/api/client";
import { useLocale } from "@/lib/i18n/LocaleProvider";
import { formatIdr, formatJakartaDateTime } from "@/lib/i18n/messages";

type FinanceTab = "journals" | "coa" | "tax";

interface DraftLine {
  key: string;
  accountId: string;
  description: string;
  debit: string;
  credit: string;
}

const ACCOUNT_CATEGORIES = [
  "HEADER_GROUP",
  "CASH_BANK",
  "BANK_OUTBOUND_CLEARING",
  "ACCOUNTS_RECEIVABLE",
  "INVENTORY_ASSET",
  "TAX_RECEIVABLE",
  "GRNI_ACCRUAL",
  "ACCOUNTS_PAYABLE",
  "TAX_PAYABLE",
  "WITHHOLDING_TAX_PAYABLE",
  "EQUITY_CAPITAL",
  "SALES_REVENUE",
  "COGS_EXPENSE",
  "PURCHASE_PRICE_VARIANCE",
  "OPERATING_EXPENSE",
  "GENERAL",
] as const;

const TAX_FAMILIES = ["PPN", "PPH"] as const;
const TAX_DIRECTIONS = ["INPUT", "OUTPUT", "WITHHOLDING"] as const;
const TAX_TREATMENTS = [
  "STANDARD_TAXABLE",
  "SPECIAL_DPP_11_12",
  "ZERO_RATED",
  "EXEMPT",
  "NOT_COLLECTED_FACILITY",
  "WITHHOLDING_DEDUCTION",
] as const;

/** Parses decimal input string cleanly to integer 1/10,000 minor units without IEEE-754 float drift. */
function parseMinorTenThousandths(raw: string): number {
  const cleaned = raw.trim();
  if (!cleaned || cleaned.startsWith("-")) return 0;
  const match = /^(\d+)(?:\.(\d*))?$/.exec(cleaned);
  if (!match) return 0;
  const whole = Number.parseInt(match[1] ?? "0", 10);
  const fracRaw = (match[2] ?? "").slice(0, 4).padEnd(4, "0");
  const frac = Number.parseInt(fracRaw, 10);
  return whole * 10_000 + frac;
}

function PeriodStatusBadge({ status, label }: { status: string; label: string }) {
  const style =
    status === "OPEN"
      ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
      : status === "LOCKED"
        ? "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
        : "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]";

  return (
    <span className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-semibold ${style}`}>
      {status === "OPEN" ? (
        <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      ) : status === "LOCKED" ? (
        <Lock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      ) : (
        <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
      )}
      <span>{label}</span>
    </span>
  );
}

function FinanceSheet({ tenantId, entityId }: { tenantId: string; entityId: string }) {
  const router = useRouter();
  const routerRef = useRef(router);
  const { locale, t } = useLocale();

  const [detail, setDetail] = useState<EntityWorkspaceDetailResponse | null>(null);
  const [accountsData, setAccountsData] = useState<GlAccountListResponse | null>(null);
  const [periodsData, setPeriodsData] = useState<FinancialPeriodListResponse | null>(null);
  const [mappingsData, setMappingsData] = useState<AccountMappingListResponse | null>(null);
  const [taxData, setTaxData] = useState<TaxCodeListResponse | null>(null);
  const [journalsData, setJournalsData] = useState<PagedJournalsResponse | null>(null);
  const [selectedJournal, setSelectedJournal] = useState<JournalDetailDto | null>(null);
  const [selectedJournalId, setSelectedJournalId] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<FinanceTab>("journals");
  const [loading, setLoading] = useState(true);
  const [problem, setProblem] = useState<ApiProblemError | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  // Filters for journals list
  const [searchText, setSearchText] = useState("");
  const [periodFilter, setPeriodFilter] = useState("");
  const [kindFilter, setKindFilter] = useState("");

  // Journal Composer state
  const [postingDate, setPostingDate] = useState("2026-11-18");
  const [sourceEventKey, setSourceEventKey] = useState("");
  const [memo, setMemo] = useState("");
  const [draftLines, setDraftLines] = useState<DraftLine[]>([
    { key: "line-1", accountId: "", description: "", debit: "250000", credit: "0" },
    { key: "line-2", accountId: "", description: "", debit: "0", credit: "250000" },
  ]);
  const [composerError, setComposerError] = useState<{
    field: "date" | "sourceKey" | "memo" | "form";
    message: string;
  } | null>(null);
  const [composerBusy, setComposerBusy] = useState(false);

  // Reversal state
  const [reversalDate, setReversalDate] = useState("2026-11-18");
  const [reversalReason, setReversalReason] = useState("");
  const [reversalError, setReversalError] = useState<{
    field: "date" | "reason" | "form";
    message: string;
  } | null>(null);
  const [reversalBusy, setReversalBusy] = useState(false);

  // New Account state
  const [newAccCode, setNewAccCode] = useState("");
  const [newAccName, setNewAccName] = useState("");
  const [newAccType, setNewAccType] = useState("EXPENSE");
  const [newAccCategory, setNewAccCategory] = useState("OPERATING_EXPENSE");
  const [newAccParentId, setNewAccParentId] = useState("");
  const [newAccAllowPosting, setNewAccAllowPosting] = useState(true);
  const [newAccControl, setNewAccControl] = useState(false);
  const [accountError, setAccountError] = useState<{
    field: "code" | "name" | "form";
    message: string;
  } | null>(null);
  const [accountTableError, setAccountTableError] = useState<string | null>(null);
  const [accountBusy, setAccountBusy] = useState(false);
  const [accountToggleBusyId, setAccountToggleBusyId] = useState<string | null>(null);

  // Mappings draft selections per purpose
  const [mappingDrafts, setMappingDrafts] = useState<Record<string, string>>({});
  const [mappingError, setMappingError] = useState<string | null>(null);
  const [mappingBusyPurpose, setMappingBusyPurpose] = useState<string | null>(null);

  // New Period & Close state
  const [newPeriodCode, setNewPeriodCode] = useState("");
  const [newFiscalYear, setNewFiscalYear] = useState("2026");
  const [newPeriodNumber, setNewPeriodNumber] = useState("12");
  const [newPeriodStart, setNewPeriodStart] = useState("2026-12-01");
  const [newPeriodEnd, setNewPeriodEnd] = useState("2026-12-31");
  const [periodError, setPeriodError] = useState<{
    field: "code" | "year" | "number" | "start" | "end" | "form";
    message: string;
  } | null>(null);
  const [periodCloseError, setPeriodCloseError] = useState<string | null>(null);
  const [periodBusy, setPeriodBusy] = useState(false);
  const [closeNote, setCloseNote] = useState("");
  const [closeBusyId, setCloseBusyId] = useState<string | null>(null);

  // Tax Code & Preview state
  const [newTaxCode, setNewTaxCode] = useState("");
  const [newTaxName, setNewTaxName] = useState("");
  const [newTaxFamily, setNewTaxFamily] = useState("PPN");
  const [newTaxDirection, setNewTaxDirection] = useState("INPUT");
  const [newTaxTreatment, setNewTaxTreatment] = useState("STANDARD_TAXABLE");
  const [newTaxRate, setNewTaxRate] = useState("11");
  const [newTaxDppNum, setNewTaxDppNum] = useState("1");
  const [newTaxDppDen, setNewTaxDppDen] = useState("1");
  const [newTaxEffFrom, setNewTaxEffFrom] = useState("2026-01-01");
  const [newTaxAccountId, setNewTaxAccountId] = useState("");
  const [newTaxReqEvidence, setNewTaxReqEvidence] = useState(true);
  const [newTaxReqNpwp, setNewTaxReqNpwp] = useState(false);
  const [newTaxNote, setNewTaxNote] = useState("");
  const [taxCreateError, setTaxCreateError] = useState<{
    field: "code" | "name" | "rate" | "dppNum" | "dppDen" | "effFrom" | "form";
    message: string;
  } | null>(null);
  const [taxCreateBusy, setTaxCreateBusy] = useState(false);

  const [previewTaxCodeId, setPreviewTaxCodeId] = useState("");
  const [previewGrossAmount, setPreviewGrossAmount] = useState("1000000");
  const [previewTransDate, setPreviewTransDate] = useState("2026-11-18");
  const [previewHasNpwp, setPreviewHasNpwp] = useState(true);
  const [previewEvidenceProvided, setPreviewEvidenceProvided] = useState(true);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [previewError, setPreviewError] = useState<{
    field: "code" | "amount" | "date" | "form";
    message: string;
  } | null>(null);
  const [previewResult, setPreviewResult] = useState<EvaluateTaxPreviewResponse | null>(null);

  useEffect(() => {
    routerRef.current = router;
  }, [router]);

  const resolveError = useCallback(
    (err: ApiProblemError) => t.finance.errors[err.errorCode] ?? err.detail ?? t.login.errors.unknown,
    [t.finance.errors, t.login.errors.unknown],
  );

  useEffect(() => {
    let active = true;

    Promise.all([
      fetchEntityWorkspaceDetail(tenantId, entityId),
      fetchGlAccounts(tenantId, entityId),
      fetchFinancialPeriods(tenantId, entityId),
      fetchAccountMappings(tenantId, entityId),
      fetchTaxCodes(tenantId, entityId),
      fetchJournals(tenantId, entityId, {
        q: searchText,
        periodId: periodFilter || undefined,
        entryKind: kindFilter || undefined,
        page: 1,
        pageSize: 40,
      }),
    ])
      .then(([entityRes, accRes, perRes, mapRes, taxRes, jrnRes]) => {
        if (!active) return;
        setDetail(entityRes);
        setAccountsData(accRes);
        setPeriodsData(perRes);
        setMappingsData(mapRes);
        setTaxData(taxRes);
        setJournalsData(jrnRes);
        setProblem(null);
        setLoadError(null);

        const postable = accRes.items.filter((a) => a.allowPosting && a.status === "ACTIVE");
        const cash = postable.find((a) => a.accountCode === "1110") ?? postable[1] ?? postable[0];
        const expense = postable.find((a) => a.accountCode === "5300") ?? postable[0];
        setDraftLines((prev) =>
          prev.map((line, idx) =>
            line.accountId
              ? line
              : {
                  ...line,
                  accountId: idx === 0 ? (expense?.accountId ?? "") : (cash?.accountId ?? ""),
                },
          ),
        );

        setMappingDrafts((prev) => {
          const next = { ...prev };
          for (const m of mapRes.items) {
            if (!next[m.purposeCode]) {
              next[m.purposeCode] = m.accountId;
            }
          }
          return next;
        });

        setPreviewTaxCodeId((prev) => {
          if (prev && taxRes.items.some((x) => x.taxCodeId === prev)) return prev;
          return taxRes.items.find((x) => x.code === "PPN-12-DPP1112")?.taxCodeId ?? taxRes.items[0]?.taxCodeId ?? "";
        });

        setSelectedJournalId((prev) => {
          if (prev && jrnRes.items.some((j) => j.journalId === prev)) return prev;
          return jrnRes.items[0]?.journalId ?? null;
        });
      })
      .catch((err) => {
        if (!active) return;
        const apiProblem = err as ApiProblemError;
        if (apiProblem.status === 401) {
          routerRef.current.replace("/login");
          return;
        }
        setProblem(apiProblem);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, searchText, periodFilter, kindFilter, reloadNonce]);

  useEffect(() => {
    if (!selectedJournalId) {
      return;
    }

    let active = true;
    fetchJournalDetail(tenantId, entityId, selectedJournalId)
      .then((res) => {
        if (!active) return;
        setSelectedJournal(res);
        setReversalError(null);
      })
      .catch((err) => {
        if (!active) return;
        setLoadError(resolveError(err as ApiProblemError));
      });

    return () => {
      active = false;
    };
  }, [tenantId, entityId, selectedJournalId, resolveError]);

  async function handlePostJournal(e: React.FormEvent) {
    e.preventDefault();
    if (!detail) return;
    if (!postingDate) {
      setComposerError({ field: "date", message: t.finance.validation.postingDateRequired });
      return;
    }
    if (!sourceEventKey.trim()) {
      setComposerError({ field: "sourceKey", message: t.finance.validation.sourceKeyRequired });
      return;
    }
    if (!memo.trim()) {
      setComposerError({ field: "memo", message: t.finance.validation.memoRequired });
      return;
    }

    setComposerBusy(true);
    setComposerError(null);
    try {
      const created = await postJournal(tenantId, entityId, {
        postingDate,
        currencyCode: detail.entity.functionalCurrencyCode,
        description: memo.trim(),
        sourceModule: "MANUAL_GL",
        sourceDocumentType: "MANUAL_JOURNAL",
        sourceDocumentId: sourceEventKey.trim(),
        sourceEventKey: sourceEventKey.trim(),
        lines: draftLines.map((l) => ({
          accountId: l.accountId,
          description: l.description.trim() || null,
          debitAmount: Number(l.debit || "0"),
          creditAmount: Number(l.credit || "0"),
          partnerId: null,
          itemId: null,
          taxCodeId: null,
        })),
      });
      setSourceEventKey("");
      setMemo("");
      setSelectedJournalId(created.journalId);
      setSelectedJournal(created);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      const field =
        apiErr.errorCode === "period_closed" || apiErr.errorCode === "period_not_found_for_date"
          ? "date"
          : apiErr.errorCode === "duplicate_posting_source_conflict"
            ? "sourceKey"
            : "form";
      setComposerError({ field, message: resolveError(apiErr) });
    } finally {
      setComposerBusy(false);
    }
  }

  async function handleReverseJournal(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedJournal) return;
    if (!reversalDate) {
      setReversalError({ field: "date", message: t.finance.validation.reversalDateRequired });
      return;
    }
    if (!reversalReason.trim()) {
      setReversalError({ field: "reason", message: t.finance.validation.reversalReasonRequired });
      return;
    }

    setReversalBusy(true);
    setReversalError(null);
    try {
      const rev = await reverseJournal(tenantId, entityId, selectedJournal.journalId, {
        reversalPostingDate: reversalDate,
        reason: reversalReason.trim(),
      });
      setReversalReason("");
      setSelectedJournalId(rev.journalId);
      setSelectedJournal(rev);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      const field =
        apiErr.errorCode === "period_closed" || apiErr.errorCode === "period_not_found_for_date"
          ? "date"
          : "form";
      setReversalError({ field, message: resolveError(apiErr) });
    } finally {
      setReversalBusy(false);
    }
  }

  async function handleCreateAccount(e: React.FormEvent) {
    e.preventDefault();
    if (newAccCode.trim().length < 2) {
      setAccountError({ field: "code", message: t.finance.validation.accountCodeRequired });
      return;
    }
    if (!newAccName.trim()) {
      setAccountError({ field: "name", message: t.finance.validation.accountNameRequired });
      return;
    }

    setAccountBusy(true);
    setAccountError(null);
    setAccountTableError(null);
    try {
      await createGlAccount(tenantId, entityId, {
        accountCode: newAccCode.trim(),
        name: newAccName.trim(),
        accountType: newAccType,
        accountCategory: newAccCategory,
        parentAccountId: newAccParentId || null,
        allowPosting: newAccCategory !== "HEADER_GROUP" && newAccAllowPosting,
        isControlAccount: newAccControl,
      });
      setNewAccCode("");
      setNewAccName("");
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setAccountError({
        field: apiErr.errorCode === "gl_account_code_conflict" ? "code" : "form",
        message: resolveError(apiErr),
      });
    } finally {
      setAccountBusy(false);
    }
  }

  async function handleToggleAccountStatus(account: GlAccountDto) {
    setAccountToggleBusyId(account.accountId);
    setAccountTableError(null);
    try {
      await updateGlAccount(tenantId, entityId, account.accountId, {
        name: account.name,
        accountCategory: account.accountCategory,
        parentAccountId: account.parentAccountId ?? null,
        allowPosting: account.allowPosting,
        isControlAccount: account.isControlAccount,
        status: account.status === "ACTIVE" ? "INACTIVE" : "ACTIVE",
        expectedVersion: account.version,
      });
      setReloadNonce((n) => n + 1);
    } catch (err) {
      setAccountTableError(resolveError(err as ApiProblemError));
    } finally {
      setAccountToggleBusyId(null);
    }
  }

  async function handleSaveMapping(purposeCode: string) {
    const accountId = mappingDrafts[purposeCode];
    if (!accountId) return;
    setMappingBusyPurpose(purposeCode);
    setMappingError(null);
    try {
      await upsertAccountMapping(tenantId, entityId, {
        purposeCode,
        accountId,
        notes: null,
      });
      setReloadNonce((n) => n + 1);
    } catch (err) {
      setMappingError(resolveError(err as ApiProblemError));
    } finally {
      setMappingBusyPurpose(null);
    }
  }

  async function handleCreatePeriod(e: React.FormEvent) {
    e.preventDefault();
    const yr = Number(newFiscalYear);
    const num = Number(newPeriodNumber);
    if (!newPeriodCode.trim()) {
      setPeriodError({ field: "code", message: t.finance.validation.periodCodeRequired });
      return;
    }
    if (!Number.isInteger(yr) || yr < 2000 || yr > 2100) {
      setPeriodError({ field: "year", message: t.finance.validation.periodYearInvalid });
      return;
    }
    if (!Number.isInteger(num) || num < 1 || num > 13) {
      setPeriodError({ field: "number", message: t.finance.validation.periodNumberInvalid });
      return;
    }
    if (!newPeriodStart) {
      setPeriodError({ field: "start", message: t.finance.validation.periodDatesInvalid });
      return;
    }
    if (!newPeriodEnd || newPeriodEnd < newPeriodStart) {
      setPeriodError({ field: "end", message: t.finance.validation.periodDatesInvalid });
      return;
    }

    setPeriodBusy(true);
    setPeriodError(null);
    setPeriodCloseError(null);
    try {
      await createFinancialPeriod(tenantId, entityId, {
        periodCode: newPeriodCode.trim(),
        fiscalYear: yr,
        periodNumber: num,
        startDate: newPeriodStart,
        endDate: newPeriodEnd,
      });
      setNewPeriodCode("");
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setPeriodError({
        field: apiErr.errorCode === "financial_period_conflict" ? "code" : "form",
        message: resolveError(apiErr),
      });
    } finally {
      setPeriodBusy(false);
    }
  }

  async function handleClosePeriod(periodId: string, hardLock: boolean) {
    setCloseBusyId(periodId);
    setPeriodCloseError(null);
    const body: CloseFinancialPeriodRequest = {
      note: closeNote.trim() || null,
      hardLock,
    };
    try {
      await closeFinancialPeriod(tenantId, entityId, periodId, body);
      setCloseNote("");
      setReloadNonce((n) => n + 1);
    } catch (err) {
      setPeriodCloseError(resolveError(err as ApiProblemError));
    } finally {
      setCloseBusyId(null);
    }
  }

  async function handleCreateTaxCode(e: React.FormEvent) {
    e.preventDefault();
    const rate = Number(newTaxRate);
    const dppNum = Number(newTaxDppNum);
    const dppDen = Number(newTaxDppDen);

    if (newTaxCode.trim().length < 2) {
      setTaxCreateError({ field: "code", message: t.finance.validation.taxCodeRequired });
      return;
    }
    if (!newTaxName.trim()) {
      setTaxCreateError({ field: "name", message: t.finance.validation.taxNameRequired });
      return;
    }
    if (Number.isNaN(rate) || rate < 0 || rate > 100) {
      setTaxCreateError({ field: "rate", message: t.finance.validation.taxRateInvalid });
      return;
    }
    if (!Number.isInteger(dppNum) || dppNum < 1) {
      setTaxCreateError({ field: "dppNum", message: t.finance.validation.taxDppInvalid });
      return;
    }
    if (!Number.isInteger(dppDen) || dppDen < dppNum) {
      setTaxCreateError({ field: "dppDen", message: t.finance.validation.taxDppInvalid });
      return;
    }
    if (!newTaxEffFrom) {
      setTaxCreateError({ field: "effFrom", message: t.finance.validation.taxEffDateRequired });
      return;
    }

    setTaxCreateBusy(true);
    setTaxCreateError(null);
    try {
      const created = await createTaxCode(tenantId, entityId, {
        code: newTaxCode.trim(),
        name: newTaxName.trim(),
        taxFamily: newTaxFamily,
        direction: newTaxDirection,
        treatmentClass: newTaxTreatment,
        nominalRatePercent: rate,
        taxableBaseRatioNumerator: dppNum,
        taxableBaseRatioDenominator: dppDen,
        requiresDocumentaryEvidence: newTaxReqEvidence,
        requiresCounterpartyNpwp: newTaxReqNpwp,
        taxAccountId: newTaxAccountId || null,
        effectiveFrom: newTaxEffFrom,
        effectiveTo: null,
        statutoryReferenceNote: newTaxNote.trim() || null,
      });
      setNewTaxCode("");
      setNewTaxName("");
      setNewTaxNote("");
      setPreviewTaxCodeId(created.taxCodeId);
      setReloadNonce((n) => n + 1);
    } catch (err) {
      const apiErr = err as ApiProblemError;
      setTaxCreateError({
        field: apiErr.errorCode === "tax_code_conflict" ? "code" : "form",
        message: resolveError(apiErr),
      });
    } finally {
      setTaxCreateBusy(false);
    }
  }

  async function handleEvaluateTaxPreview(e: React.FormEvent) {
    e.preventDefault();
    const gross = Number(previewGrossAmount);
    if (Number.isNaN(gross) || gross <= 0) {
      setPreviewResult(null);
      setPreviewError({ field: "amount", message: t.finance.validation.taxPreviewAmountInvalid });
      return;
    }
    if (!previewTransDate) {
      setPreviewResult(null);
      setPreviewError({ field: "date", message: t.finance.validation.postingDateRequired });
      return;
    }

    setPreviewBusy(true);
    setPreviewError(null);
    try {
      const res = await evaluateTaxPreview(tenantId, entityId, {
        taxCodeId: previewTaxCodeId || null,
        grossAmount: gross,
        transactionDate: previewTransDate,
        hasCounterpartyNpwp: previewHasNpwp,
        evidenceProvided: previewEvidenceProvided,
      });
      setPreviewResult(res);
    } catch (err) {
      setPreviewResult(null);
      const apiErr = err as ApiProblemError;
      const field =
        apiErr.errorCode === "explicit_tax_code_required" || apiErr.errorCode === "foreign_or_missing_tax_code"
          ? "code"
          : apiErr.errorCode === "tax_code_not_effective_on_date"
            ? "date"
            : "form";
      setPreviewError({ field, message: resolveError(apiErr) });
    } finally {
      setPreviewBusy(false);
    }
  }

  function handleTabKeyDown(e: React.KeyboardEvent, current: FinanceTab) {
    const order: FinanceTab[] = ["journals", "coa", "tax"];
    const idx = order.indexOf(current);
    let nextTab: FinanceTab | null = null;
    if (e.key === "ArrowRight") {
      nextTab = order[(idx + 1) % order.length]!;
    } else if (e.key === "ArrowLeft") {
      nextTab = order[(idx - 1 + order.length) % order.length]!;
    } else if (e.key === "Home") {
      nextTab = order[0]!;
    } else if (e.key === "End") {
      nextTab = order[order.length - 1]!;
    }

    if (nextTab) {
      e.preventDefault();
      setActiveTab(nextTab);
      document.getElementById(`tab-btn-${nextTab}`)?.focus();
    }
  }

  if (loading && !detail) {
    return <ScopedLoadingSheet />;
  }

  if (problem || !detail || !accountsData || !periodsData || !mappingsData || !taxData || !journalsData) {
    return (
      <ScopedBoundaryError
        problem={problem}
        onRetry={() => {
          setLoading(true);
          setReloadNonce((n) => n + 1);
        }}
      />
    );
  }

  const canManageConfig = detail.effectivePermissions.includes("finance.config.manage");
  const canPost = detail.effectivePermissions.includes("finance.journal.post");
  const postableAccounts = accountsData.items.filter((a) => a.allowPosting && a.status === "ACTIVE");
  const headerAccounts = accountsData.items.filter((a) => a.accountCategory === "HEADER_GROUP");

  const draftDebitMinor = draftLines.reduce((sum, l) => sum + parseMinorTenThousandths(l.debit), 0);
  const draftCreditMinor = draftLines.reduce((sum, l) => sum + parseMinorTenThousandths(l.credit), 0);
  const draftDeltaMinor = Math.abs(draftDebitMinor - draftCreditMinor);
  const draftDebitTotal = draftDebitMinor / 10_000;
  const draftCreditTotal = draftCreditMinor / 10_000;
  const draftDelta = draftDeltaMinor / 10_000;
  const isDraftBalanced = draftDebitMinor > 0 && draftDeltaMinor === 0;

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--border-hairline)] pb-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-[var(--text-primary)]">{t.finance.heading}</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            <span className="font-code font-semibold tabular-nums text-[var(--text-primary)]">
              {detail.tenant.code} · {detail.entity.code} · {detail.entity.functionalCurrencyCode}
            </span>
            {" — "}
            <span>{t.finance.lead}</span>
          </p>
        </div>

        <Link
          href={`/w/${tenantId}/${entityId}`}
          className="inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
        >
          <ArrowLeft className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
          <span>
            {t.finance.backToEntity} · <span className="font-code">{detail.entity.code}</span>
          </span>
        </Link>
      </div>

      {!canManageConfig && !canPost ? (
        <div className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-4 py-3 text-xs text-[var(--status-warn-text)]">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.finance.readOnlyNotice}</span>
        </div>
      ) : null}

      <div className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5 text-xs text-[var(--text-secondary)]">
        <div className="flex items-start gap-2">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.finance.governanceNotice}</span>
        </div>
        <div className="flex flex-wrap gap-1.5">
          {periodsData.items.slice(0, 3).map((p) => (
            <PeriodStatusBadge
              key={p.periodId}
              status={p.status}
              label={`${p.periodCode} · ${t.finance.periodStatusLabels[p.status] ?? p.status}`}
            />
          ))}
        </div>
      </div>

      {loadError ? (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-4 py-3 text-sm text-[var(--status-danger-text)]"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{loadError}</span>
        </div>
      ) : null}

      <div
        role="tablist"
        aria-label={t.finance.heading}
        className="flex flex-wrap gap-1.5 border-b border-[var(--border-hairline)] pb-2"
      >
        <button
          id="tab-btn-journals"
          type="button"
          role="tab"
          aria-selected={activeTab === "journals"}
          aria-controls="panel-journals"
          onClick={() => setActiveTab("journals")}
          onKeyDown={(e) => handleTabKeyDown(e, "journals")}
          className={`inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border px-3 text-xs font-semibold transition-colors ${
            activeTab === "journals"
              ? "border-[var(--accent-primary)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
              : "border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          <BookOpen className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.finance.tabJournals}</span>
        </button>

        <button
          id="tab-btn-coa"
          type="button"
          role="tab"
          aria-selected={activeTab === "coa"}
          aria-controls="panel-coa"
          onClick={() => setActiveTab("coa")}
          onKeyDown={(e) => handleTabKeyDown(e, "coa")}
          className={`inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border px-3 text-xs font-semibold transition-colors ${
            activeTab === "coa"
              ? "border-[var(--accent-primary)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
              : "border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          <Landmark className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.finance.tabCoaMappings}</span>
        </button>

        <button
          id="tab-btn-tax"
          type="button"
          role="tab"
          aria-selected={activeTab === "tax"}
          aria-controls="panel-tax"
          onClick={() => setActiveTab("tax")}
          onKeyDown={(e) => handleTabKeyDown(e, "tax")}
          className={`inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border px-3 text-xs font-semibold transition-colors ${
            activeTab === "tax"
              ? "border-[var(--accent-primary)] bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
              : "border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
          }`}
        >
          <CalendarClock className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <span>{t.finance.tabPeriodsTax}</span>
        </button>
      </div>

      {activeTab === "journals" ? (
        <div id="panel-journals" role="tabpanel" aria-labelledby="tab-btn-journals" className="space-y-6">
          {canPost ? (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
                <div className="flex items-center gap-2">
                  <FileSpreadsheet className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                  <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.composerTitle}</h2>
                </div>
                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-warn-text)]">
                  <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                  <span>{t.finance.draftBadge}</span>
                </span>
              </div>

              <form onSubmit={handlePostJournal} noValidate className="space-y-4 p-4">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-12">
                  <div className="sm:col-span-3">
                    <label htmlFor="journal-date" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.finance.postingDateLabel}
                    </label>
                    <input
                      id="journal-date"
                      type="date"
                      value={postingDate}
                      onChange={(e) => {
                        setPostingDate(e.target.value);
                        if (composerError) setComposerError(null);
                      }}
                      aria-invalid={composerError?.field === "date"}
                      aria-describedby={composerError?.field === "date" ? "composer-error" : undefined}
                      className={`font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm tabular-nums text-[var(--text-primary)] ${
                        composerError?.field === "date"
                          ? "border-[var(--status-danger-text)]"
                          : "border-[var(--border-strong)]"
                      }`}
                    />
                  </div>

                  <div className="sm:col-span-4">
                    <label htmlFor="journal-source-key" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.finance.sourceKeyLabel}
                    </label>
                    <input
                      id="journal-source-key"
                      type="text"
                      value={sourceEventKey}
                      onChange={(e) => {
                        setSourceEventKey(e.target.value);
                        if (composerError) setComposerError(null);
                      }}
                      placeholder={t.finance.sourceKeyPlaceholder}
                      aria-invalid={composerError?.field === "sourceKey"}
                      aria-describedby={composerError?.field === "sourceKey" ? "composer-error" : undefined}
                      className={`font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                        composerError?.field === "sourceKey"
                          ? "border-[var(--status-danger-text)]"
                          : "border-[var(--border-strong)]"
                      }`}
                    />
                  </div>

                  <div className="sm:col-span-5">
                    <label htmlFor="journal-memo" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.finance.memoLabel}
                    </label>
                    <input
                      id="journal-memo"
                      type="text"
                      value={memo}
                      onChange={(e) => {
                        setMemo(e.target.value);
                        if (composerError) setComposerError(null);
                      }}
                      placeholder={t.finance.memoPlaceholder}
                      aria-invalid={composerError?.field === "memo"}
                      aria-describedby={composerError?.field === "memo" ? "composer-error" : undefined}
                      className={`mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                        composerError?.field === "memo"
                          ? "border-[var(--status-danger-text)]"
                          : "border-[var(--border-strong)]"
                      }`}
                    />
                  </div>
                </div>

                <div className="space-y-2.5 border-t border-[var(--border-hairline)] pt-3">
                  {draftLines.map((line, idx) => (
                    <div key={line.key} className="grid grid-cols-1 gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-2.5 sm:grid-cols-12">
                      <div className="sm:col-span-4">
                        <label htmlFor={`draft-acc-${idx}`} className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.lineAccountLabel} #{idx + 1}
                        </label>
                        <select
                          id={`draft-acc-${idx}`}
                          value={line.accountId}
                          onChange={(e) => {
                            const val = e.target.value;
                            setDraftLines((prev) => prev.map((x, i) => (i === idx ? { ...x, accountId: val } : x)));
                            if (composerError) setComposerError(null);
                          }}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {postableAccounts.map((acc) => (
                            <option key={acc.accountId} value={acc.accountId}>
                              {acc.accountCode} — {acc.name}
                            </option>
                          ))}
                        </select>
                      </div>

                      <div className="sm:col-span-3">
                        <label htmlFor={`draft-desc-${idx}`} className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.lineDescLabel}
                        </label>
                        <input
                          id={`draft-desc-${idx}`}
                          type="text"
                          value={line.description}
                          onChange={(e) => {
                            const val = e.target.value;
                            setDraftLines((prev) => prev.map((x, i) => (i === idx ? { ...x, description: val } : x)));
                          }}
                          placeholder={t.finance.lineDescPlaceholder}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                        />
                      </div>

                      <div className="sm:col-span-2">
                        <label htmlFor={`draft-debit-${idx}`} className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.lineDebitLabel}
                        </label>
                        <input
                          id={`draft-debit-${idx}`}
                          type="number"
                          min="0"
                          step="0.01"
                          value={line.debit}
                          onChange={(e) => {
                            const val = e.target.value;
                            setDraftLines((prev) => prev.map((x, i) => (i === idx ? { ...x, debit: val } : x)));
                            if (composerError) setComposerError(null);
                          }}
                          className="font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums text-[var(--text-primary)]"
                        />
                      </div>

                      <div className="sm:col-span-2">
                        <label htmlFor={`draft-credit-${idx}`} className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.lineCreditLabel}
                        </label>
                        <input
                          id={`draft-credit-${idx}`}
                          type="number"
                          min="0"
                          step="0.01"
                          value={line.credit}
                          onChange={(e) => {
                            const val = e.target.value;
                            setDraftLines((prev) => prev.map((x, i) => (i === idx ? { ...x, credit: val } : x)));
                            if (composerError) setComposerError(null);
                          }}
                          className="font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums text-[var(--text-primary)]"
                        />
                      </div>

                      <div className="flex items-end sm:col-span-1">
                        {draftLines.length > 2 ? (
                          <button
                            type="button"
                            aria-label={`${t.finance.removeLineLabel} #${idx + 1}`}
                            onClick={() => setDraftLines((prev) => prev.filter((_, i) => i !== idx))}
                            className="inline-flex h-9 w-full items-center justify-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-secondary)] hover:bg-[var(--status-danger-bg)] hover:text-[var(--status-danger-text)]"
                          >
                            <Trash2 className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                          </button>
                        ) : (
                          <span className="hidden sm:block sm:h-9 sm:w-full" aria-hidden="true" />
                        )}
                      </div>
                    </div>
                  ))}
                </div>

                <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3.5 py-2.5 text-xs">
                  <div className="flex flex-wrap items-center gap-4">
                    <div>
                      <span className="text-[var(--text-secondary)]">{t.finance.totalDebitLabel}: </span>
                      <span className="font-code font-semibold tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                        {formatIdr(draftDebitTotal)}
                      </span>
                    </div>
                    <div>
                      <span className="text-[var(--text-secondary)]">{t.finance.totalCreditLabel}: </span>
                      <span className="font-code font-semibold tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                        {formatIdr(draftCreditTotal)}
                      </span>
                    </div>
                    <div>
                      <span className="text-[var(--text-secondary)]">{t.finance.deltaLabel}: </span>
                      <span
                        data-testid="draft-balance-delta"
                        className={`font-code font-semibold tabular-nums whitespace-nowrap ${
                          isDraftBalanced ? "text-[var(--status-ok-text)]" : "text-[var(--status-danger-text)]"
                        }`}
                      >
                        {formatIdr(draftDelta)}
                      </span>
                    </div>
                  </div>
                  <span
                    data-testid="draft-balance-badge"
                    className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-semibold ${
                      isDraftBalanced
                        ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                        : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
                    }`}
                  >
                    {isDraftBalanced ? (
                      <CheckCircle2 className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    ) : (
                      <AlertCircle className="h-3.5 w-3.5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    )}
                    <span>{isDraftBalanced ? t.finance.balancedTag : t.finance.unbalancedTag}</span>
                  </span>
                </div>

                {composerError ? (
                  <p id="composer-error" role="alert" className="text-xs font-medium text-[var(--status-danger-text)]">
                    {composerError.message}
                  </p>
                ) : null}

                <div className="flex flex-wrap items-center gap-3">
                  <button
                    type="button"
                    onClick={() =>
                      setDraftLines((prev) => [
                        ...prev,
                        {
                          key: `line-${Date.now()}-${prev.length + 1}`,
                          accountId: postableAccounts[0]?.accountId ?? "",
                          description: "",
                          debit: "0",
                          credit: "0",
                        },
                      ])
                    }
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                  >
                    <Plus className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                    <span>{t.finance.addLineButton}</span>
                  </button>

                  <button
                    type="submit"
                    disabled={composerBusy}
                    className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    {composerBusy ? t.finance.postJournalBusy : t.finance.postJournalButton}
                  </button>
                </div>
              </form>
            </section>
          ) : null}

          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.ledgerTitle}</h2>
              <span className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                {Number(journalsData.totalCount)}
              </span>
            </div>

            <div className="grid grid-cols-1 gap-3 border-b border-[var(--border-hairline)] p-4 sm:grid-cols-12">
              <div className="sm:col-span-6">
                <input
                  aria-label={t.finance.searchPlaceholder}
                  type="search"
                  value={searchText}
                  onChange={(e) => setSearchText(e.target.value)}
                  placeholder={t.finance.searchPlaceholder}
                  className="h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)]"
                />
              </div>
              <div className="sm:col-span-3">
                <select
                  aria-label={t.finance.periodFilterAll}
                  value={periodFilter}
                  onChange={(e) => setPeriodFilter(e.target.value)}
                  className="h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                >
                  <option value="">{t.finance.periodFilterAll}</option>
                  {periodsData.items.map((p) => (
                    <option key={p.periodId} value={p.periodId}>
                      {p.periodCode}
                    </option>
                  ))}
                </select>
              </div>
              <div className="sm:col-span-3">
                <select
                  aria-label={t.finance.kindFilterAll}
                  value={kindFilter}
                  onChange={(e) => setKindFilter(e.target.value)}
                  className="h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                >
                  <option value="">{t.finance.kindFilterAll}</option>
                  <option value="STANDARD">{t.finance.postedBadge}</option>
                  <option value="REVERSAL">{t.finance.reversalEntryBadge}</option>
                </select>
              </div>
            </div>

            {journalsData.items.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-[var(--text-secondary)]">{t.finance.emptyJournals}</div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-sm">
                  <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] text-xs font-semibold text-[var(--text-secondary)] md:table-header-group">
                    <tr>
                      <th scope="col" className="px-4 py-2.5">{t.finance.colJournalNumber}</th>
                      <th scope="col" className="px-4 py-2.5">{t.finance.colDatePeriod}</th>
                      <th scope="col" className="px-4 py-2.5">{t.finance.colDescriptionSource}</th>
                      <th scope="col" className="px-4 py-2.5 text-right">{t.finance.colDebitCredit}</th>
                      <th scope="col" className="px-4 py-2.5 text-right">{t.finance.colStateAction}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {journalsData.items.map((j) => {
                      const isSelected = selectedJournalId === j.journalId;
                      return (
                        <tr
                          key={j.journalId}
                          className={`block border-b border-[var(--border-hairline)] p-4 last:border-b-0 md:table-row md:p-0 ${
                            isSelected ? "bg-[var(--accent-tint)]" : ""
                          }`}
                        >
                          <td className="block pb-1.5 md:table-cell md:px-4 md:py-3 md:align-top">
                            <div className="font-code text-xs font-semibold whitespace-nowrap text-[var(--text-primary)]">
                              {j.journalNumber}
                            </div>
                            <div className="mt-1">
                              {j.entryKind === "REVERSAL" ? (
                                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-warn-text)]">
                                  <RotateCcw className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                  <span>{t.finance.reversalEntryBadge}</span>
                                </span>
                              ) : j.isReversed ? (
                                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-danger-text)]">
                                  <RotateCcw className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                  <span>{t.finance.reversedBadge}</span>
                                </span>
                              ) : (
                                <span className="inline-flex items-center gap-1 rounded-[var(--radius-sm)] border border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-ok-text)]">
                                  <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                                  <span>{t.finance.postedBadge}</span>
                                </span>
                              )}
                            </div>
                          </td>

                          <td className="block pb-1.5 md:table-cell md:px-4 md:py-3 md:align-top">
                            <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.finance.colDatePeriod}:
                            </span>
                            <div className="font-code text-xs tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                              {j.postingDate}
                            </div>
                            <div className="font-code text-[11px] whitespace-nowrap text-[var(--text-secondary)]">
                              {j.periodCode}
                            </div>
                          </td>

                          <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:align-top">
                            <div className="text-xs font-medium text-[var(--text-primary)]">{j.description}</div>
                            <div className="font-code mt-0.5 text-[11px] text-[var(--text-secondary)]">
                              {j.sourceModule} · {j.sourceEventKey}
                            </div>
                          </td>

                          <td className="block pb-2 md:table-cell md:px-4 md:py-3 md:text-right md:align-top">
                            <span className="mr-1.5 text-xs font-medium text-[var(--text-secondary)] md:hidden">
                              {t.finance.colDebitCredit}:
                            </span>
                            <div className="font-code text-xs font-semibold tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                              {formatIdr(Number(j.totalDebit))}
                            </div>
                            <div className="text-[11px] text-[var(--text-secondary)]">
                              {Number(j.lineCount)} {t.finance.linesUnitLabel}
                            </div>
                          </td>

                          <td className="block md:table-cell md:px-4 md:py-3 md:text-right md:align-top">
                            <button
                              type="button"
                              aria-label={`${canPost ? t.finance.manageButton : t.finance.viewOnlyButton} (${j.journalNumber})`}
                              onClick={() => setSelectedJournalId(j.journalId)}
                              className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                            >
                              {canPost ? t.finance.manageButton : t.finance.viewOnlyButton}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {selectedJournal ? (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-strong)] bg-[var(--bg-surface)]">
              <div className="flex flex-wrap items-start justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
                <div>
                  <h2 className="text-xs font-semibold text-[var(--text-primary)]">
                    {t.finance.detailTitle} —{" "}
                    <span className="font-code whitespace-nowrap">{selectedJournal.journalNumber}</span>
                  </h2>
                  <p className="font-code mt-0.5 text-[11px] tabular-nums text-[var(--text-secondary)]">
                    {formatJakartaDateTime(selectedJournal.postedAtUtc, locale)}
                  </p>
                </div>
                <div className="space-y-1 text-right text-[11px] text-[var(--text-secondary)]">
                  <div>
                    <span>{t.finance.sourceTraceLabel}: </span>
                    <span className="font-code text-[var(--text-primary)]">
                      {selectedJournal.sourceModule} / {selectedJournal.sourceEventKey}
                    </span>
                  </div>
                  <div>
                    <span>{t.finance.payloadHashLabel}: </span>
                    <span className="font-code text-[var(--text-primary)]" title={selectedJournal.payloadHash}>
                      {selectedJournal.payloadHash.slice(0, 16)}…
                    </span>
                  </div>
                </div>
              </div>

              <div className="space-y-4 p-4">
                {selectedJournal.reversalOfJournalId || selectedJournal.reversedByJournalId ? (
                  <div className="flex flex-wrap items-center gap-2 rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-3 py-2 text-xs">
                    {selectedJournal.reversalOfJournalId ? (
                      <button
                        type="button"
                        onClick={() => setSelectedJournalId(selectedJournal.reversalOfJournalId!)}
                        className="inline-flex min-h-8 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                      >
                        <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.finance.viewOriginalJournalButton}</span>
                      </button>
                    ) : null}
                    {selectedJournal.reversedByJournalId ? (
                      <button
                        type="button"
                        onClick={() => setSelectedJournalId(selectedJournal.reversedByJournalId!)}
                        className="inline-flex min-h-8 items-center gap-1.5 rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                      >
                        <RotateCcw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                        <span>{t.finance.viewReversalJournalButton}</span>
                      </button>
                    ) : null}
                  </div>
                ) : null}

                <div className="overflow-x-auto rounded-[var(--radius-sm)] border border-[var(--border-hairline)]">
                  <table className="w-full border-collapse text-left text-xs">
                    <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold text-[var(--text-secondary)] sm:table-header-group">
                      <tr>
                        <th scope="col" className="px-3 py-2">{t.finance.colLineAccount}</th>
                        <th scope="col" className="px-3 py-2">{t.finance.colLineMemo}</th>
                        <th scope="col" className="px-3 py-2 text-right">{t.finance.colLineDebit}</th>
                        <th scope="col" className="px-3 py-2 text-right">{t.finance.colLineCredit}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedJournal.lines.map((line) => (
                        <tr key={line.lineId} className="block border-b border-[var(--border-hairline)] p-3 last:border-b-0 sm:table-row sm:p-0">
                          <td className="block pb-1 sm:table-cell sm:px-3 sm:py-2">
                            <span className="font-code font-semibold whitespace-nowrap text-[var(--text-primary)]">
                              {line.accountCode}
                            </span>{" "}
                            <span className="text-[var(--text-secondary)]">{line.accountName}</span>
                          </td>
                          <td className="block pb-1 text-[var(--text-secondary)] sm:table-cell sm:px-3 sm:py-2">
                            {line.description ?? "—"}
                          </td>
                          <td className="block pb-0.5 sm:table-cell sm:px-3 sm:py-2 sm:text-right">
                            <span className="mr-1.5 font-medium text-[var(--text-secondary)] sm:hidden">
                              {t.finance.colLineDebit}:
                            </span>
                            <span className="font-code tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                              {Number(line.debitAmount) > 0 ? formatIdr(Number(line.debitAmount)) : "—"}
                            </span>
                          </td>
                          <td className="block sm:table-cell sm:px-3 sm:py-2 sm:text-right">
                            <span className="mr-1.5 font-medium text-[var(--text-secondary)] sm:hidden">
                              {t.finance.colLineCredit}:
                            </span>
                            <span className="font-code tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                              {Number(line.creditAmount) > 0 ? formatIdr(Number(line.creditAmount)) : "—"}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot className="border-t border-[var(--border-hairline)] bg-[var(--bg-subtle)] font-semibold">
                      <tr className="block p-3 sm:table-row sm:p-0">
                        <td colSpan={2} className="block pb-1 text-[var(--text-primary)] sm:table-cell sm:px-3 sm:py-2">
                          {t.finance.totalDebitLabel} / {t.finance.totalCreditLabel}
                        </td>
                        <td className="block pb-0.5 sm:table-cell sm:px-3 sm:py-2 sm:text-right">
                          <span className="mr-1.5 font-medium text-[var(--text-secondary)] sm:hidden">
                            {t.finance.totalDebitLabel}:
                          </span>
                          <span className="font-code tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                            {formatIdr(Number(selectedJournal.totalDebit))}
                          </span>
                        </td>
                        <td className="block sm:table-cell sm:px-3 sm:py-2 sm:text-right">
                          <span className="mr-1.5 font-medium text-[var(--text-secondary)] sm:hidden">
                            {t.finance.totalCreditLabel}:
                          </span>
                          <span className="font-code tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                            {formatIdr(Number(selectedJournal.totalCredit))}
                          </span>
                        </td>
                      </tr>
                    </tfoot>
                  </table>
                </div>

                {canPost && selectedJournal.entryKind === "STANDARD" && !selectedJournal.isReversed ? (
                  <form
                    onSubmit={handleReverseJournal}
                    noValidate
                    className="space-y-3 rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-subtle)] p-3.5"
                  >
                    <h3 className="flex items-center gap-1.5 text-xs font-semibold text-[var(--text-primary)]">
                      <RotateCcw className="h-3.5 w-3.5 text-[var(--status-danger-text)]" strokeWidth={1.75} aria-hidden="true" />
                      <span>{t.finance.reverseSectionTitle}</span>
                    </h3>
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-12">
                      <div className="sm:col-span-3">
                        <label htmlFor="reversal-date" className="block text-xs font-semibold text-[var(--text-secondary)]">
                          {t.finance.reverseDateLabel}
                        </label>
                        <input
                          id="reversal-date"
                          type="date"
                          value={reversalDate}
                          onChange={(e) => {
                            setReversalDate(e.target.value);
                            if (reversalError) setReversalError(null);
                          }}
                          aria-invalid={reversalError?.field === "date"}
                          aria-describedby={reversalError?.field === "date" ? "reversal-error" : undefined}
                          className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] ${
                            reversalError?.field === "date" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                      </div>
                      <div className="sm:col-span-6">
                        <label htmlFor="reversal-reason" className="block text-xs font-semibold text-[var(--text-secondary)]">
                          {t.finance.reverseReasonLabel}
                        </label>
                        <input
                          id="reversal-reason"
                          type="text"
                          value={reversalReason}
                          onChange={(e) => {
                            setReversalReason(e.target.value);
                            if (reversalError) setReversalError(null);
                          }}
                          placeholder={t.finance.reverseReasonPlaceholder}
                          aria-invalid={reversalError?.field === "reason"}
                          aria-describedby={reversalError?.field === "reason" ? "reversal-error" : undefined}
                          className={`mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)] ${
                            reversalError?.field === "reason" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                      </div>
                      <div className="flex items-end sm:col-span-3">
                        <button
                          type="submit"
                          disabled={reversalBusy}
                          className="inline-flex min-h-9 w-full items-center justify-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-3 text-xs font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                        >
                          {reversalBusy ? t.finance.reverseBusy : t.finance.reverseButton}
                        </button>
                      </div>
                    </div>
                    {reversalError ? (
                      <p id="reversal-error" role="alert" className="text-xs font-medium text-[var(--status-danger-text)]">
                        {reversalError.message}
                      </p>
                    ) : null}
                  </form>
                ) : null}
              </div>
            </section>
          ) : null}
        </div>
      ) : null}

      {activeTab === "coa" ? (
        <div id="panel-coa" role="tabpanel" aria-labelledby="tab-btn-coa" className="space-y-6">
          {canManageConfig ? (
            <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
              <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
                <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.addAccountTitle}</h2>
              </div>
              <form onSubmit={handleCreateAccount} noValidate className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-12">
                <div className="sm:col-span-3">
                  <label htmlFor="new-acc-code" className="block text-xs font-semibold text-[var(--text-secondary)]">
                    {t.finance.accountCodeLabel}
                  </label>
                  <input
                    id="new-acc-code"
                    type="text"
                    value={newAccCode}
                    onChange={(e) => {
                      setNewAccCode(e.target.value);
                      if (accountError) setAccountError(null);
                    }}
                    placeholder="5310"
                    aria-invalid={accountError?.field === "code"}
                    aria-describedby={accountError?.field === "code" ? "account-create-error" : undefined}
                    className={`font-code mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm uppercase text-[var(--text-primary)] ${
                      accountError?.field === "code" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                    }`}
                  />
                </div>
                <div className="sm:col-span-5">
                  <label htmlFor="new-acc-name" className="block text-xs font-semibold text-[var(--text-secondary)]">
                    {t.finance.accountNameLabel}
                  </label>
                  <input
                    id="new-acc-name"
                    type="text"
                    value={newAccName}
                    onChange={(e) => {
                      setNewAccName(e.target.value);
                      if (accountError) setAccountError(null);
                    }}
                    placeholder="Beban Sewa Gudang"
                    aria-invalid={accountError?.field === "name"}
                    aria-describedby={accountError?.field === "name" ? "account-create-error" : undefined}
                    className={`mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-3 text-sm text-[var(--text-primary)] ${
                      accountError?.field === "name" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                    }`}
                  />
                </div>
                <div className="sm:col-span-4">
                  <label htmlFor="new-acc-type" className="block text-xs font-semibold text-[var(--text-secondary)]">
                    {t.finance.accountTypeLabel}
                  </label>
                  <select
                    id="new-acc-type"
                    value={newAccType}
                    onChange={(e) => setNewAccType(e.target.value)}
                    className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                  >
                    {["ASSET", "LIABILITY", "EQUITY", "REVENUE", "EXPENSE"].map((typ) => (
                      <option key={typ} value={typ}>
                        {t.finance.accountTypeLabels[typ] ?? typ}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="sm:col-span-4">
                  <label htmlFor="new-acc-category" className="block text-xs font-semibold text-[var(--text-secondary)]">
                    {t.finance.accountCategoryLabel}
                  </label>
                  <select
                    id="new-acc-category"
                    value={newAccCategory}
                    onChange={(e) => setNewAccCategory(e.target.value)}
                    className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                  >
                    {ACCOUNT_CATEGORIES.map((cat) => (
                      <option key={cat} value={cat}>
                        {t.finance.accountCategoryLabels[cat] ?? cat}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="sm:col-span-4">
                  <label htmlFor="new-acc-parent" className="block text-xs font-semibold text-[var(--text-secondary)]">
                    {t.finance.parentAccountLabel}
                  </label>
                  <select
                    id="new-acc-parent"
                    value={newAccParentId}
                    onChange={(e) => setNewAccParentId(e.target.value)}
                    className="mt-1.5 h-10 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)]"
                  >
                    <option value="">{t.finance.noParentOption}</option>
                    {headerAccounts.map((h) => (
                      <option key={h.accountId} value={h.accountId}>
                        {h.accountCode} — {h.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="flex flex-col justify-end gap-1 sm:col-span-4">
                  <label className="flex cursor-pointer items-center gap-2 text-xs text-[var(--text-primary)]">
                    <input
                      type="checkbox"
                      checked={newAccCategory !== "HEADER_GROUP" && newAccAllowPosting}
                      disabled={newAccCategory === "HEADER_GROUP"}
                      onChange={(e) => setNewAccAllowPosting(e.target.checked)}
                      className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                    />
                    <span>{t.finance.allowPostingCheck}</span>
                  </label>
                  <label className="flex cursor-pointer items-center gap-2 text-xs text-[var(--text-primary)]">
                    <input
                      type="checkbox"
                      checked={newAccControl}
                      onChange={(e) => setNewAccControl(e.target.checked)}
                      className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                    />
                    <span>{t.finance.controlAccountCheck}</span>
                  </label>
                </div>

                {accountError ? (
                  <p id="account-create-error" role="alert" className="text-xs font-medium text-[var(--status-danger-text)] sm:col-span-12">
                    {accountError.message}
                  </p>
                ) : null}

                <div className="sm:col-span-12">
                  <button
                    type="submit"
                    disabled={accountBusy}
                    className="inline-flex min-h-10 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-4 text-sm font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    {accountBusy ? t.finance.createAccountBusy : t.finance.createAccountButton}
                  </button>
                </div>
              </form>
            </section>
          ) : null}

          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex items-center justify-between border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.coaTitle}</h2>
              <span className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                {Number(accountsData.items.length)}
              </span>
            </div>
            {accountTableError ? (
              <p id="account-table-error" role="alert" className="border-b border-[var(--border-hairline)] bg-[var(--status-danger-bg)] px-4 py-2.5 text-xs text-[var(--status-danger-text)]">
                {accountTableError}
              </p>
            ) : null}
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="px-4 py-2.5">{t.finance.accountCodeLabel} &amp; {t.finance.accountNameLabel}</th>
                    <th scope="col" className="px-4 py-2.5">{t.finance.accountTypeLabel}</th>
                    <th scope="col" className="px-4 py-2.5">{t.finance.accountCategoryLabel}</th>
                    <th scope="col" className="px-4 py-2.5 text-right">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {accountsData.items.map((acc) => {
                    const isHeader = acc.accountCategory === "HEADER_GROUP";
                    return (
                      <tr
                        key={acc.accountId}
                        className={`block border-b border-[var(--border-hairline)] p-3 last:border-b-0 md:table-row md:p-0 ${
                          isHeader ? "bg-[var(--bg-subtle)] font-semibold" : ""
                        }`}
                      >
                        <td className="block pb-1 md:table-cell md:px-4 md:py-2.5">
                          <span className={`font-code inline-flex items-center whitespace-nowrap text-[var(--text-primary)] ${acc.parentAccountId ? "md:pl-4" : ""}`}>
                            {acc.parentAccountId ? (
                              <CornerDownRight className="mr-1 h-3.5 w-3.5 shrink-0 text-[var(--text-secondary)]" strokeWidth={1.75} aria-hidden="true" />
                            ) : null}
                            {acc.accountCode}
                          </span>{" "}
                          <span className="text-[var(--text-primary)]">{acc.name}</span>
                        </td>
                        <td className="block pb-1 text-[var(--text-secondary)] md:table-cell md:px-4 md:py-2.5">
                          {t.finance.accountTypeLabels[acc.accountType] ?? acc.accountType} ·{" "}
                          {t.finance.normalBalanceLabels[acc.normalBalance] ?? acc.normalBalance}
                        </td>
                        <td className="block pb-1.5 md:table-cell md:px-4 md:py-2.5">
                          <div className="flex flex-wrap gap-1.5">
                            <span className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] px-2 py-0.5 text-[11px] text-[var(--text-secondary)]">
                              {isHeader ? t.finance.headerGroupTag : t.finance.postableTag}
                            </span>
                            {acc.isControlAccount ? (
                              <span className="rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] px-2 py-0.5 text-[11px] font-semibold text-[var(--status-warn-text)]">
                                {t.finance.controlTag}
                              </span>
                            ) : null}
                          </div>
                        </td>
                        <td className="block md:table-cell md:px-4 md:py-2.5 md:text-right">
                          <div className="flex flex-wrap items-center gap-2 md:justify-end">
                            <span
                              className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-2 py-0.5 text-[11px] font-semibold ${
                                acc.status === "ACTIVE"
                                  ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                                  : "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
                              }`}
                            >
                              {acc.status === "ACTIVE" ? (
                                <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              ) : (
                                <AlertCircle className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                              )}
                              <span>{acc.status === "ACTIVE" ? t.partners.statusLabels.ACTIVE : t.partners.statusLabels.INACTIVE}</span>
                            </span>
                            {canManageConfig ? (
                              <button
                                type="button"
                                disabled={accountToggleBusyId === acc.accountId}
                                onClick={() => handleToggleAccountStatus(acc)}
                                className="inline-flex min-h-8 items-center rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-[11px] font-semibold text-[var(--text-primary)] hover:bg-[var(--bg-chrome)] disabled:opacity-60"
                              >
                                {acc.status === "ACTIVE" ? t.finance.deactivateAccountButton : t.finance.activateAccountButton}
                              </button>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </section>

          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.mappingsTitle}</h2>
            </div>
            {mappingError ? (
              <p role="alert" className="border-b border-[var(--border-hairline)] bg-[var(--status-danger-bg)] px-4 py-2.5 text-xs text-[var(--status-danger-text)]">
                {mappingError}
              </p>
            ) : null}
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="px-4 py-2.5">{t.finance.colPurpose}</th>
                    <th scope="col" className="px-4 py-2.5">{t.finance.colMappedAccount}</th>
                    {canManageConfig ? <th scope="col" className="px-4 py-2.5 text-right">{t.finance.colMappingAction}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {mappingsData.requiredPurposes.map((purpose) => {
                    const current = mappingsData.items.find((m) => m.purposeCode === purpose);
                    const draftAccountId = mappingDrafts[purpose] ?? current?.accountId ?? "";
                    const isDirty = Boolean(draftAccountId && draftAccountId !== (current?.accountId ?? ""));
                    return (
                      <tr key={purpose} className="block border-b border-[var(--border-hairline)] p-3 last:border-b-0 md:table-row md:p-0">
                        <td className="block pb-1.5 md:table-cell md:px-4 md:py-2.5">
                          <div className="font-semibold text-[var(--text-primary)]">
                            {t.finance.mappingPurposeLabels[purpose] ?? purpose}
                          </div>
                          <div className="font-code text-[11px] text-[var(--text-secondary)]">{purpose}</div>
                        </td>
                        <td className="block pb-2 md:table-cell md:px-4 md:py-2.5">
                          {canManageConfig ? (
                            <select
                              aria-label={t.finance.mappingPurposeLabels[purpose] ?? purpose}
                              value={draftAccountId}
                              onChange={(e) => {
                                const val = e.target.value;
                                setMappingDrafts((prev) => ({ ...prev, [purpose]: val }));
                                if (mappingError) setMappingError(null);
                              }}
                              className="h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                            >
                              {postableAccounts.map((acc) => (
                                <option key={acc.accountId} value={acc.accountId}>
                                  {acc.accountCode} — {acc.name} ({t.finance.accountTypeLabels[acc.accountType] ?? acc.accountType})
                                </option>
                              ))}
                            </select>
                          ) : current ? (
                            <span className="font-code whitespace-nowrap text-[var(--text-primary)]">
                              {current.accountCode} — {current.accountName}
                            </span>
                          ) : (
                            <span>—</span>
                          )}
                        </td>
                        {canManageConfig ? (
                          <td className="block md:table-cell md:px-4 md:py-2.5 md:text-right">
                            <button
                              type="button"
                              disabled={mappingBusyPurpose === purpose}
                              onClick={() => handleSaveMapping(purpose)}
                              className={`inline-flex min-h-9 items-center rounded-[var(--radius-sm)] px-3 text-xs font-semibold disabled:opacity-60 ${
                                isDirty
                                  ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)]"
                                  : "border border-[var(--border-strong)] bg-[var(--bg-surface)] text-[var(--text-primary)] hover:bg-[var(--bg-chrome)]"
                              }`}
                            >
                              {t.finance.saveMappingButton}
                            </button>
                          </td>
                        ) : null}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      ) : null}

      {activeTab === "tax" ? (
        <div id="panel-tax" role="tabpanel" aria-labelledby="tab-btn-tax" className="space-y-6">
          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.periodsTitle}</h2>
            </div>

            {canManageConfig ? (
              <div className="space-y-4 border-b border-[var(--border-hairline)] p-4">
                <form onSubmit={handleCreatePeriod} noValidate className="space-y-3">
                  <h3 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.addPeriodTitle}</h3>
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-12">
                    <div className="sm:col-span-2">
                      <label htmlFor="new-period-code" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.periodCodeLabel}
                      </label>
                      <input
                        id="new-period-code"
                        type="text"
                        value={newPeriodCode}
                        onChange={(e) => {
                          setNewPeriodCode(e.target.value);
                          if (periodError) setPeriodError(null);
                        }}
                        placeholder="2026-12"
                        aria-invalid={periodError?.field === "code"}
                        aria-describedby={periodError?.field === "code" ? "period-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm text-[var(--text-primary)] ${
                          periodError?.field === "code" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div className="sm:col-span-2">
                      <label htmlFor="new-fiscal-year" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.fiscalYearLabel}
                      </label>
                      <input
                        id="new-fiscal-year"
                        type="number"
                        value={newFiscalYear}
                        onChange={(e) => {
                          setNewFiscalYear(e.target.value);
                          if (periodError) setPeriodError(null);
                        }}
                        aria-invalid={periodError?.field === "year"}
                        aria-describedby={periodError?.field === "year" ? "period-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] ${
                          periodError?.field === "year" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div className="sm:col-span-2">
                      <label htmlFor="new-period-number" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.periodNumberLabel}
                      </label>
                      <input
                        id="new-period-number"
                        type="number"
                        min="1"
                        max="13"
                        value={newPeriodNumber}
                        onChange={(e) => {
                          setNewPeriodNumber(e.target.value);
                          if (periodError) setPeriodError(null);
                        }}
                        aria-invalid={periodError?.field === "number"}
                        aria-describedby={periodError?.field === "number" ? "period-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] ${
                          periodError?.field === "number" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div className="sm:col-span-2">
                      <label htmlFor="new-period-start" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.startDateLabel}
                      </label>
                      <input
                        id="new-period-start"
                        type="date"
                        value={newPeriodStart}
                        onChange={(e) => {
                          setNewPeriodStart(e.target.value);
                          if (periodError) setPeriodError(null);
                        }}
                        aria-invalid={periodError?.field === "start"}
                        aria-describedby={periodError?.field === "start" ? "period-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)] ${
                          periodError?.field === "start" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div className="sm:col-span-2">
                      <label htmlFor="new-period-end" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.endDateLabel}
                      </label>
                      <input
                        id="new-period-end"
                        type="date"
                        value={newPeriodEnd}
                        onChange={(e) => {
                          setNewPeriodEnd(e.target.value);
                          if (periodError) setPeriodError(null);
                        }}
                        aria-invalid={periodError?.field === "end"}
                        aria-describedby={periodError?.field === "end" ? "period-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)] ${
                          periodError?.field === "end" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div className="flex items-end sm:col-span-2">
                      <button
                        type="submit"
                        disabled={periodBusy}
                        className="inline-flex min-h-9 w-full items-center justify-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                      >
                        {periodBusy ? t.finance.createPeriodBusy : t.finance.createPeriodButton}
                      </button>
                    </div>
                  </div>
                  {periodError ? (
                    <p id="period-error" role="alert" className="text-xs font-medium text-[var(--status-danger-text)]">
                      {periodError.message}
                    </p>
                  ) : null}
                </form>

                <div id="period-close-strip" className="space-y-2 border-t border-[var(--border-hairline)] pt-3">
                  <div className="flex flex-wrap items-end gap-3">
                    <div className="min-w-[240px] flex-1">
                      <label htmlFor="period-close-note" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.periodCloseStripTitle} — {t.finance.closePeriodNoteLabel}
                      </label>
                      <input
                        id="period-close-note"
                        type="text"
                        value={closeNote}
                        onChange={(e) => setCloseNote(e.target.value)}
                        placeholder={t.finance.closePeriodNotePlaceholder}
                        className="mt-1 h-9 w-full max-w-md rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                      />
                    </div>
                  </div>
                  {periodCloseError ? (
                    <p role="alert" className="text-xs font-medium text-[var(--status-danger-text)]">
                      {periodCloseError}
                    </p>
                  ) : null}
                </div>
              </div>
            ) : null}

            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead className="hidden border-b border-[var(--border-hairline)] bg-[var(--bg-surface)] font-semibold text-[var(--text-secondary)] md:table-header-group">
                  <tr>
                    <th scope="col" className="px-4 py-2.5">{t.finance.periodCodeLabel}</th>
                    <th scope="col" className="px-4 py-2.5">{t.finance.startDateLabel} – {t.finance.endDateLabel}</th>
                    <th scope="col" className="px-4 py-2.5">Status</th>
                    {canManageConfig ? <th scope="col" className="px-4 py-2.5 text-right">{t.finance.colMappingAction}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {periodsData.items.map((p) => (
                    <tr key={p.periodId} className="block border-b border-[var(--border-hairline)] p-3 last:border-b-0 md:table-row md:p-0">
                      <td className="block pb-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="mr-1.5 font-medium text-[var(--text-secondary)] md:hidden">
                          {t.finance.periodCodeLabel}:
                        </span>
                        <span className="font-code font-semibold whitespace-nowrap text-[var(--text-primary)]">
                          {p.periodCode}
                        </span>
                      </td>
                      <td className="block pb-1 md:table-cell md:px-4 md:py-2.5">
                        <span className="mr-1.5 font-medium text-[var(--text-secondary)] md:hidden">
                          {t.finance.dateRangeMobileLabel}:
                        </span>
                        <span className="font-code tabular-nums whitespace-nowrap text-[var(--text-secondary)]">
                          {p.startDate} .. {p.endDate}
                        </span>
                      </td>
                      <td className="block pb-1.5 md:table-cell md:px-4 md:py-2.5">
                        <span className="mr-1.5 font-medium text-[var(--text-secondary)] md:hidden">Status:</span>
                        <PeriodStatusBadge status={p.status} label={t.finance.periodStatusLabels[p.status] ?? p.status} />
                      </td>
                      {canManageConfig ? (
                        <td className="block md:table-cell md:px-4 md:py-2.5 md:text-right">
                          {p.status === "OPEN" ? (
                            <div className="flex flex-wrap gap-2 md:justify-end">
                              <button
                                type="button"
                                disabled={closeBusyId === p.periodId}
                                onClick={() => handleClosePeriod(p.periodId, false)}
                                className="inline-flex min-h-8 items-center rounded-[var(--radius-sm)] border border-[var(--status-warn-border)] bg-[var(--bg-surface)] px-2.5 text-[11px] font-semibold text-[var(--status-warn-text)] hover:bg-[var(--status-warn-bg)] disabled:opacity-60"
                              >
                                {t.finance.closePeriodButton}
                              </button>
                              <button
                                type="button"
                                disabled={closeBusyId === p.periodId}
                                onClick={() => handleClosePeriod(p.periodId, true)}
                                className="inline-flex min-h-8 items-center rounded-[var(--radius-sm)] border border-[var(--status-danger-border)] bg-[var(--bg-surface)] px-2.5 text-[11px] font-semibold text-[var(--status-danger-text)] hover:bg-[var(--status-danger-bg)] disabled:opacity-60"
                              >
                                {t.finance.lockPeriodButton}
                              </button>
                            </div>
                          ) : (
                            <span className="text-[11px] text-[var(--text-muted)]">—</span>
                          )}
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="overflow-hidden rounded-[var(--radius-md)] border border-[var(--border-hairline)] bg-[var(--bg-surface)]">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border-hairline)] bg-[var(--bg-subtle)] px-4 py-2.5">
              <div className="flex items-center gap-2">
                <Receipt className="h-4 w-4 text-[var(--accent-primary)]" strokeWidth={1.75} aria-hidden="true" />
                <h2 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.taxCodesTitle}</h2>
              </div>
              <span className="font-code text-xs tabular-nums text-[var(--text-secondary)]">
                {Number(taxData.items.length)}
              </span>
            </div>

            <div className="grid grid-cols-1 divide-y divide-[var(--border-hairline)] lg:grid-cols-2 lg:divide-x lg:divide-y-0">
              <div className="space-y-4 p-4">
                <h3 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.taxPreviewTitle}</h3>
                <form onSubmit={handleEvaluateTaxPreview} noValidate className="space-y-3">
                  <div>
                    <label htmlFor="tax-preview-code" className="block text-xs font-semibold text-[var(--text-secondary)]">
                      {t.finance.taxPreviewCodeLabel}
                    </label>
                    <select
                      id="tax-preview-code"
                      value={previewTaxCodeId}
                      onChange={(e) => {
                        setPreviewTaxCodeId(e.target.value);
                        if (previewError) setPreviewError(null);
                      }}
                      aria-invalid={previewError?.field === "code"}
                      aria-describedby={previewError?.field === "code" ? "tax-preview-error" : undefined}
                      className={`mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] ${
                        previewError?.field === "code" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                      }`}
                    >
                      <option value="">{t.finance.selectTaxCodeOption}</option>
                      {taxData.items.map((tc) => (
                        <option key={tc.taxCodeId} value={tc.taxCodeId}>
                          {tc.code} — {tc.name}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <label htmlFor="tax-preview-amount" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.taxPreviewAmountLabel}
                      </label>
                      <input
                        id="tax-preview-amount"
                        type="number"
                        min="1"
                        step="1"
                        value={previewGrossAmount}
                        onChange={(e) => {
                          setPreviewGrossAmount(e.target.value);
                          if (previewError) setPreviewError(null);
                        }}
                        aria-invalid={previewError?.field === "amount"}
                        aria-describedby={previewError?.field === "amount" ? "tax-preview-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-right text-sm tabular-nums text-[var(--text-primary)] ${
                          previewError?.field === "amount" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                    <div>
                      <label htmlFor="tax-preview-date" className="block text-xs font-semibold text-[var(--text-secondary)]">
                        {t.finance.taxPreviewDateLabel}
                      </label>
                      <input
                        id="tax-preview-date"
                        type="date"
                        value={previewTransDate}
                        onChange={(e) => {
                          setPreviewTransDate(e.target.value);
                          if (previewError) setPreviewError(null);
                        }}
                        aria-invalid={previewError?.field === "date"}
                        aria-describedby={previewError?.field === "date" ? "tax-preview-error" : undefined}
                        className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-sm tabular-nums text-[var(--text-primary)] ${
                          previewError?.field === "date" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                        }`}
                      />
                    </div>
                  </div>

                  <div className="space-y-1.5">
                    <label className="flex cursor-pointer items-center gap-2 text-xs text-[var(--text-primary)]">
                      <input
                        id="tax-preview-npwp"
                        type="checkbox"
                        checked={previewHasNpwp}
                        onChange={(e) => setPreviewHasNpwp(e.target.checked)}
                        className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                      />
                      <span>{t.finance.taxPreviewNpwpCheck}</span>
                    </label>
                    <label className="flex cursor-pointer items-center gap-2 text-xs text-[var(--text-primary)]">
                      <input
                        id="tax-preview-evidence"
                        type="checkbox"
                        checked={previewEvidenceProvided}
                        onChange={(e) => setPreviewEvidenceProvided(e.target.checked)}
                        className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                      />
                      <span>{t.finance.taxPreviewEvidenceCheck}</span>
                    </label>
                  </div>

                  <button
                    type="submit"
                    disabled={previewBusy}
                    className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                  >
                    {previewBusy ? t.finance.taxPreviewBusy : t.finance.taxPreviewButton}
                  </button>

                  {previewError ? (
                    <p id="tax-preview-error" role="alert" className="text-xs font-medium text-[var(--status-danger-text)]">
                      {previewError.message}
                    </p>
                  ) : null}

                  {previewResult ? (
                    <div
                      data-testid="tax-preview-result"
                      className={`space-y-1 rounded-[var(--radius-sm)] border p-3 text-xs ${
                        previewResult.isBlocked
                          ? "border-[var(--status-danger-border)] bg-[var(--status-danger-bg)] text-[var(--status-danger-text)]"
                          : "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                      }`}
                    >
                      <div>
                        {t.finance.taxPreviewBaseLabel}:{" "}
                        <span className="font-code font-semibold tabular-nums whitespace-nowrap">
                          {formatIdr(Number(previewResult.taxableBaseAmount))}
                        </span>
                      </div>
                      <div>
                        {t.finance.taxPreviewTaxLabel}:{" "}
                        <span className="font-code font-semibold tabular-nums whitespace-nowrap">
                          {formatIdr(Number(previewResult.taxAmount))}
                        </span>
                      </div>
                      <div>
                        {t.finance.taxPreviewRateLabel}:{" "}
                        <span className="font-code font-semibold tabular-nums whitespace-nowrap">
                          {Number(previewResult.effectiveRatePercent)}%
                        </span>
                      </div>
                      {previewResult.isBlocked ? (
                        <div className="font-semibold">
                          {t.finance.taxPreviewBlockedLabel}: {previewResult.blockedReason}
                        </div>
                      ) : null}
                    </div>
                  ) : null}
                </form>
              </div>

              <div className="space-y-3 p-4">
                <ul className="space-y-2">
                  {taxData.items.map((tc) => (
                    <li
                      key={tc.taxCodeId}
                      className="rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-subtle)] p-2.5 text-xs"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="font-code font-semibold whitespace-nowrap text-[var(--text-primary)]">{tc.code}</span>
                        <span className="font-code tabular-nums whitespace-nowrap text-[var(--text-primary)]">
                          {Number(tc.nominalRatePercent)}% ({Number(tc.taxableBaseRatioNumerator)}/{Number(tc.taxableBaseRatioDenominator)} DPP)
                        </span>
                      </div>
                      <div className="mt-0.5 text-[var(--text-secondary)]">{tc.name}</div>
                      <div className="mt-1 flex flex-wrap gap-1.5">
                        <span
                          className={`inline-flex items-center gap-1 rounded-[var(--radius-sm)] border px-1.5 py-0.5 text-[11px] font-medium ${
                            tc.reviewStatus === "REVIEWED_INTERNAL"
                              ? "border-[var(--status-ok-border)] bg-[var(--status-ok-bg)] text-[var(--status-ok-text)]"
                              : "border-[var(--status-warn-border)] bg-[var(--status-warn-bg)] text-[var(--status-warn-text)]"
                          }`}
                        >
                          {tc.reviewStatus === "REVIEWED_INTERNAL" ? (
                            <CheckCircle2 className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          ) : (
                            <Clock className="h-3 w-3 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                          )}
                          <span>{t.finance.taxReviewLabels[tc.reviewStatus] ?? tc.reviewStatus}</span>
                        </span>
                      </div>
                    </li>
                  ))}
                </ul>

                {canManageConfig ? (
                  <form onSubmit={handleCreateTaxCode} noValidate className="space-y-2.5 border-t border-[var(--border-hairline)] pt-3">
                    <h3 className="text-xs font-semibold text-[var(--text-primary)]">{t.finance.addTaxCodeTitle}</h3>
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                      <div>
                        <label htmlFor="new-tax-code" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxCodeLabel}
                        </label>
                        <input
                          id="new-tax-code"
                          type="text"
                          value={newTaxCode}
                          onChange={(e) => {
                            setNewTaxCode(e.target.value);
                            if (taxCreateError) setTaxCreateError(null);
                          }}
                          placeholder="PPN-OUT-0"
                          aria-invalid={taxCreateError?.field === "code"}
                          aria-describedby={taxCreateError?.field === "code" ? "tax-create-error" : undefined}
                          className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-xs uppercase text-[var(--text-primary)] ${
                            taxCreateError?.field === "code" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                      </div>
                      <div>
                        <label htmlFor="new-tax-name" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxNameLabel}
                        </label>
                        <input
                          id="new-tax-name"
                          type="text"
                          value={newTaxName}
                          onChange={(e) => {
                            setNewTaxName(e.target.value);
                            if (taxCreateError) setTaxCreateError(null);
                          }}
                          placeholder="PPN Ekspor 0%"
                          aria-invalid={taxCreateError?.field === "name"}
                          aria-describedby={taxCreateError?.field === "name" ? "tax-create-error" : undefined}
                          className={`mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)] ${
                            taxCreateError?.field === "name" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                      </div>
                      <div>
                        <label htmlFor="new-tax-family" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxFamilyLabel}
                        </label>
                        <select
                          id="new-tax-family"
                          value={newTaxFamily}
                          onChange={(e) => setNewTaxFamily(e.target.value)}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {TAX_FAMILIES.map((fam) => (
                            <option key={fam} value={fam}>
                              {t.finance.taxFamilyLabels[fam] ?? fam}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label htmlFor="new-tax-direction" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxDirectionLabel}
                        </label>
                        <select
                          id="new-tax-direction"
                          value={newTaxDirection}
                          onChange={(e) => setNewTaxDirection(e.target.value)}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {TAX_DIRECTIONS.map((dir) => (
                            <option key={dir} value={dir}>
                              {t.finance.taxDirectionLabels[dir] ?? dir}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label htmlFor="new-tax-treatment" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxTreatmentLabel}
                        </label>
                        <select
                          id="new-tax-treatment"
                          value={newTaxTreatment}
                          onChange={(e) => setNewTaxTreatment(e.target.value)}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          {TAX_TREATMENTS.map((trt) => (
                            <option key={trt} value={trt}>
                              {t.finance.taxTreatmentLabels[trt] ?? trt}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label htmlFor="new-tax-rate" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.nominalRateLabel}
                        </label>
                        <input
                          id="new-tax-rate"
                          type="number"
                          min="0"
                          max="100"
                          step="0.01"
                          value={newTaxRate}
                          onChange={(e) => {
                            setNewTaxRate(e.target.value);
                            if (taxCreateError) setTaxCreateError(null);
                          }}
                          aria-invalid={taxCreateError?.field === "rate"}
                          aria-describedby={taxCreateError?.field === "rate" ? "tax-create-error" : undefined}
                          className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2.5 text-right text-xs tabular-nums text-[var(--text-primary)] ${
                            taxCreateError?.field === "rate" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                      </div>
                      <div>
                        <label htmlFor="new-tax-dpp-num" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.dppNumeratorLabel} / {t.finance.dppDenominatorLabel}
                        </label>
                        <div className="mt-1 flex gap-1.5">
                          <input
                            id="new-tax-dpp-num"
                            type="number"
                            min="1"
                            value={newTaxDppNum}
                            onChange={(e) => {
                              setNewTaxDppNum(e.target.value);
                              if (taxCreateError) setTaxCreateError(null);
                            }}
                            aria-invalid={taxCreateError?.field === "dppNum"}
                            aria-describedby={taxCreateError?.field === "dppNum" ? "tax-create-error" : undefined}
                            className={`font-code h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)] ${
                              taxCreateError?.field === "dppNum" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                            }`}
                          />
                          <input
                            id="new-tax-dpp-den"
                            aria-label={t.finance.dppDenominatorLabel}
                            type="number"
                            min="1"
                            value={newTaxDppDen}
                            onChange={(e) => {
                              setNewTaxDppDen(e.target.value);
                              if (taxCreateError) setTaxCreateError(null);
                            }}
                            aria-invalid={taxCreateError?.field === "dppDen"}
                            aria-describedby={taxCreateError?.field === "dppDen" ? "tax-create-error" : undefined}
                            className={`font-code h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)] ${
                              taxCreateError?.field === "dppDen" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                            }`}
                          />
                        </div>
                      </div>
                      <div>
                        <label htmlFor="new-tax-eff-from" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.effectiveFromLabel}
                        </label>
                        <input
                          id="new-tax-eff-from"
                          type="date"
                          value={newTaxEffFrom}
                          onChange={(e) => {
                            setNewTaxEffFrom(e.target.value);
                            if (taxCreateError) setTaxCreateError(null);
                          }}
                          aria-invalid={taxCreateError?.field === "effFrom"}
                          aria-describedby={taxCreateError?.field === "effFrom" ? "tax-create-error" : undefined}
                          className={`font-code mt-1 h-9 w-full rounded-[var(--radius-sm)] border bg-[var(--bg-surface)] px-2 text-xs tabular-nums text-[var(--text-primary)] ${
                            taxCreateError?.field === "effFrom" ? "border-[var(--status-danger-text)]" : "border-[var(--border-strong)]"
                          }`}
                        />
                      </div>
                      <div className="sm:col-span-2">
                        <label htmlFor="new-tax-account" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxAccountLabel}
                        </label>
                        <select
                          id="new-tax-account"
                          value={newTaxAccountId}
                          onChange={(e) => setNewTaxAccountId(e.target.value)}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2 text-xs text-[var(--text-primary)]"
                        >
                          <option value="">{t.finance.noAccountOption}</option>
                          {postableAccounts.map((a) => (
                            <option key={a.accountId} value={a.accountId}>
                              {a.accountCode} — {a.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="sm:col-span-2">
                        <label htmlFor="new-tax-note" className="block text-[11px] font-semibold text-[var(--text-secondary)]">
                          {t.finance.taxNoteLabel}
                        </label>
                        <input
                          id="new-tax-note"
                          type="text"
                          value={newTaxNote}
                          onChange={(e) => setNewTaxNote(e.target.value)}
                          className="mt-1 h-9 w-full rounded-[var(--radius-sm)] border border-[var(--border-strong)] bg-[var(--bg-surface)] px-2.5 text-xs text-[var(--text-primary)]"
                        />
                      </div>
                    </div>

                    <div className="flex flex-wrap gap-4 text-xs text-[var(--text-primary)]">
                      <label className="flex cursor-pointer items-center gap-2">
                        <input
                          type="checkbox"
                          checked={newTaxReqEvidence}
                          onChange={(e) => setNewTaxReqEvidence(e.target.checked)}
                          className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                        />
                        <span>{t.finance.requiresEvidenceCheck}</span>
                      </label>
                      <label className="flex cursor-pointer items-center gap-2">
                        <input
                          type="checkbox"
                          checked={newTaxReqNpwp}
                          onChange={(e) => setNewTaxReqNpwp(e.target.checked)}
                          className="h-4 w-4 rounded accent-[var(--accent-primary)]"
                        />
                        <span>{t.finance.requiresNpwpCheck}</span>
                      </label>
                    </div>

                    {taxCreateError ? (
                      <p id="tax-create-error" role="alert" className="text-xs font-medium text-[var(--status-danger-text)]">
                        {taxCreateError.message}
                      </p>
                    ) : null}

                    <button
                      type="submit"
                      disabled={taxCreateBusy}
                      className="inline-flex min-h-9 items-center rounded-[var(--radius-sm)] bg-[var(--accent-primary)] px-3.5 text-xs font-semibold text-[var(--accent-primary-ink)] hover:bg-[var(--accent-primary-hover)] disabled:opacity-60"
                    >
                      {taxCreateBusy ? t.finance.createTaxBusy : t.finance.createTaxButton}
                    </button>
                  </form>
                ) : null}
              </div>
            </div>
          </section>
        </div>
      ) : null}
    </div>
  );
}

export default function FinancePage() {
  const params = useParams<{ tenantId: string; entityId: string }>();
  const tenantId = typeof params?.tenantId === "string" ? params.tenantId : "";
  const entityId = typeof params?.entityId === "string" ? params.entityId : "";

  return <FinanceSheet key={`${tenantId}:${entityId}`} tenantId={tenantId} entityId={entityId} />;
}
