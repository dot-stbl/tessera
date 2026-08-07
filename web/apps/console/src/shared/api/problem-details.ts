/**
 * RFC 9457 ProblemDetails, and the error type the client throws for a non-2xx
 * response. The backend runs a ProblemDetails pipeline for every failure —
 * provider timeouts arrive as 504, an unreachable Victoria as 502 — so the UI
 * can say which upstream broke instead of "request failed".
 */

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Distributed-trace id of the failing request; the backend includes it. */
  traceId?: string;
  [extension: string]: unknown;
}

/** Thrown for any non-2xx response. Carries the parsed body when there is one. */
export class TesseraApiError extends Error {
  public readonly status: number;
  public readonly problem: ProblemDetails | undefined;

  public constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'TesseraApiError';
    this.status = status;
    this.problem = problem;
  }

  /** True when the upstream provider, not Tessera itself, is the failure. */
  public get isUpstream(): boolean {
    return this.status === 502 || this.status === 503 || this.status === 504;
  }
}

/**
 * Builds the error for a failed response. Reads the body once, prefers the
 * ProblemDetails `detail`/`title` for the message, and falls back to raw text
 * so a non-JSON failure (a proxy's HTML error page) is still legible.
 */
export async function toApiError(response: Response): Promise<TesseraApiError> {
  const raw = await response.text().catch(() => '');

  let problem: ProblemDetails | undefined;
  if (raw && response.headers.get('content-type')?.includes('json')) {
    try {
      problem = JSON.parse(raw) as ProblemDetails;
    } catch {
      problem = undefined;
    }
  }

  const message =
    problem?.detail ??
    problem?.title ??
    (raw ? raw.slice(0, 300) : `${response.status} ${response.statusText}`);

  return new TesseraApiError(response.status, message, problem);
}
