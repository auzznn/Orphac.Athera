import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

const DEFAULT_API_UPSTREAM = "http://127.0.0.1:5200";
const FORWARDED_REQUEST_HEADERS = [
  "accept",
  "content-type",
  "cookie",
  "x-csrf-token",
  "idempotency-key",
  "origin",
  "referer",
] as const;

async function proxyRequest(
  request: NextRequest,
  context: { params: Promise<{ path: string[] }> },
): Promise<NextResponse> {
  const { path } = await context.params;
  const upstreamBase = (process.env.ATHERA_API_UPSTREAM ?? DEFAULT_API_UPSTREAM).replace(/\/$/, "");
  const targetUrl = new URL(`${upstreamBase}/api/${path.join("/")}`);
  targetUrl.search = request.nextUrl.search;

  const headers = new Headers();
  for (const name of FORWARDED_REQUEST_HEADERS) {
    const value = request.headers.get(name);
    if (value) {
      headers.set(name, value);
    }
  }

  const hasBody = request.method !== "GET" && request.method !== "HEAD";
  const body = hasBody && request.body ? await request.arrayBuffer() : undefined;

  let upstreamResponse: Response;
  try {
    upstreamResponse = await fetch(targetUrl, {
      method: request.method,
      headers,
      body,
      cache: "no-store",
      redirect: "manual",
    });
  } catch {
    return NextResponse.json(
      {
        status: 503,
        title: "API upstream unavailable",
        detail: "The Orphac Athera API service is unreachable.",
        errorCode: "upstream_unavailable",
      },
      {
        status: 503,
        headers: {
          "Cache-Control": "no-store, no-cache, private",
          "Content-Type": "application/problem+json",
        },
      },
    );
  }

  const responseHeaders = new Headers();
  upstreamResponse.headers.forEach((value, key) => {
    const lower = key.toLowerCase();
    if (lower !== "set-cookie" && lower !== "content-encoding" && lower !== "content-length" && lower !== "transfer-encoding") {
      responseHeaders.set(key, value);
    }
  });

  const setCookies = typeof upstreamResponse.headers.getSetCookie === "function"
    ? upstreamResponse.headers.getSetCookie()
    : [];
  for (const cookie of setCookies) {
    responseHeaders.append("set-cookie", cookie);
  }

  responseHeaders.set("Cache-Control", "no-store, no-cache, private");
  responseHeaders.set("Pragma", "no-cache");

  const payload = await upstreamResponse.arrayBuffer();
  return new NextResponse(payload, {
    status: upstreamResponse.status,
    statusText: upstreamResponse.statusText,
    headers: responseHeaders,
  });
}

export const GET = proxyRequest;
export const POST = proxyRequest;
export const PUT = proxyRequest;
export const PATCH = proxyRequest;
export const DELETE = proxyRequest;
