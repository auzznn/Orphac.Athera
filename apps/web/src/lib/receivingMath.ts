import type { SupportedLocale } from "@/lib/i18n/messages";

export const QTY_SCALE = 6;
const SCALE_DIVISOR = BigInt(10 ** QTY_SCALE);

function expandScientificDecimal(text: string): string | null {
  const sci = /^(\d+)(?:[.,](\d+))?[eE]([+-]?\d+)$/.exec(text);
  if (!sci) return text;

  const intPart = sci[1] ?? "0";
  const fracPart = sci[2] ?? "";
  const exp = Number.parseInt(sci[3] ?? "0", 10);
  if (!Number.isFinite(exp) || Math.abs(exp) > 30) return null;

  const digits = `${intPart}${fracPart}`;
  const decimalPos = intPart.length + exp;

  if (decimalPos <= 0) {
    return `0.${"0".repeat(-decimalPos)}${digits}`;
  }
  if (decimalPos >= digits.length) {
    return `${digits}${"0".repeat(decimalPos - digits.length)}`;
  }
  return `${digits.slice(0, decimalPos)}.${digits.slice(decimalPos)}`;
}

/**
 * Parses a decimal string or number (including scientific notation like 1e-6) into a non-negative BigInt scaled by 10^scale.
 * Avoids floating-point rounding drift across UI conservation and OD-01 checks.
 */
export function parseScaledBigInt(
  raw: string | number | null | undefined,
  scale = QTY_SCALE,
): bigint | null {
  if (raw === null || raw === undefined) return null;
  if (typeof raw === "number" && (!Number.isFinite(raw) || raw < 0)) return null;
  const rawText = typeof raw === "number" ? String(raw) : raw.trim();
  if (!rawText) return null;

  const text = expandScientificDecimal(rawText);
  if (!text) return null;

  const match = /^(\d+)(?:[.,](\d+))?$/.exec(text);
  if (!match) return null;

  const wholePart = match[1] ?? "0";
  const fracRaw = match[2] ?? "";
  if (fracRaw.length > scale) return null;

  const fracPadded = fracRaw.padEnd(scale, "0");
  return BigInt(`${wholePart}${fracPadded}`);
}

function formatThousandsGroup(wholeText: string, groupSep: string): string {
  let out = "";
  let count = 0;
  for (let i = wholeText.length - 1; i >= 0; i--) {
    if (count > 0 && count % 3 === 0) {
      out = `${groupSep}${out}`;
    }
    out = `${wholeText[i]}${out}`;
    count++;
  }
  return out;
}

/**
 * Formats a scaled non-negative BigInt into a localized decimal display string,
 * trimming trailing fractional zeros while preserving exact precision.
 */
export function formatScaledBigInt(
  scaled: bigint,
  scale = QTY_SCALE,
  locale: SupportedLocale = "id-ID",
): string {
  const divisor = BigInt(10 ** scale);
  const whole = scaled / divisor;
  const frac = scaled % divisor;
  const groupSep = locale === "id-ID" ? "." : ",";
  const decSep = locale === "id-ID" ? "," : ".";
  const wholeFormatted = formatThousandsGroup(whole.toString(), groupSep);

  if (frac === 0n) return wholeFormatted;
  const fracTrimmed = frac.toString().padStart(scale, "0").replace(/0+$/, "");
  return `${wholeFormatted}${decSep}${fracTrimmed}`;
}

/**
 * Formats an API quantity (number or string) into localized display text via exact BigInt scaling.
 */
export function formatQuantityValue(
  raw: string | number | null | undefined,
  locale: SupportedLocale = "id-ID",
  scale = QTY_SCALE,
): string {
  const parsed = parseScaledBigInt(raw, scale);
  if (parsed === null) return "0";
  return formatScaledBigInt(parsed, scale, locale);
}

/**
 * Formats a scaled BigInt into a plain dot-decimal string for HTML inputs and JSON payloads.
 */
export function formatScaledPlainInput(scaled: bigint, scale = QTY_SCALE): string {
  const divisor = BigInt(10 ** scale);
  const whole = scaled / divisor;
  const frac = scaled % divisor;
  if (frac === 0n) return whole.toString();
  const fracTrimmed = frac.toString().padStart(scale, "0").replace(/0+$/, "");
  return `${whole.toString()}.${fracTrimmed}`;
}

export interface ArrivalLineConversionPreview {
  validFormat: boolean;
  baseQty: bigint | null;
  exceedsRemainingBase: bigint;
}

/**
 * Converts a PO arrival line quantity to base UoM in scaled BigInt arithmetic and reports OD-01 excess.
 */
export function convertArrivalQtyToBase(params: {
  qtyRaw: string;
  factorToBase: number | string;
  remainingEligibleBase: number | string;
}): ArrivalLineConversionPreview {
  const qtyScaled = parseScaledBigInt(params.qtyRaw);
  const factorScaled = parseScaledBigInt(params.factorToBase);
  const remainingScaled = parseScaledBigInt(params.remainingEligibleBase);

  if (qtyScaled === null || factorScaled === null || remainingScaled === null) {
    return { validFormat: false, baseQty: null, exceedsRemainingBase: 0n };
  }

  const baseQty = (qtyScaled * factorScaled) / SCALE_DIVISOR;
  const exceedsRemainingBase = baseQty > remainingScaled ? baseQty - remainingScaled : 0n;
  return { validFormat: true, baseQty, exceedsRemainingBase };
}

export interface DockCheckBalanceEvaluation {
  validFormat: boolean;
  balanced: boolean;
  sumBase: bigint | null;
  exceedsOd01Cap: boolean;
  belowValuedAccepted: boolean;
  hasHoldOrReject: boolean;
}

/**
 * Evaluates Gate 1 conservation (pending + held + released + rejected === delivered) and OD-01 release cap.
 */
export function evaluateDockCheckPreview(params: {
  deliveredBase: number | string;
  valuedAcceptedBase: number | string;
  maxReleaseCapBase: number | string;
  pendingRaw: string;
  heldRaw: string;
  releasedRaw: string;
  rejectedRaw: string;
}): DockCheckBalanceEvaluation {
  const delivered = parseScaledBigInt(params.deliveredBase);
  const valued = parseScaledBigInt(params.valuedAcceptedBase);
  const maxCap = parseScaledBigInt(params.maxReleaseCapBase);
  const pending = parseScaledBigInt(params.pendingRaw);
  const held = parseScaledBigInt(params.heldRaw);
  const released = parseScaledBigInt(params.releasedRaw);
  const rejected = parseScaledBigInt(params.rejectedRaw);

  if (
    delivered === null ||
    valued === null ||
    maxCap === null ||
    pending === null ||
    held === null ||
    released === null ||
    rejected === null
  ) {
    return {
      validFormat: false,
      balanced: false,
      sumBase: null,
      exceedsOd01Cap: false,
      belowValuedAccepted: false,
      hasHoldOrReject: false,
    };
  }

  const sumBase = pending + held + released + rejected;
  return {
    validFormat: true,
    balanced: sumBase === delivered,
    sumBase,
    exceedsOd01Cap: released > maxCap,
    belowValuedAccepted: released < valued,
    hasHoldOrReject: held > 0n || rejected > 0n,
  };
}
