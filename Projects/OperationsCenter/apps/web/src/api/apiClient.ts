export type ApiErrorKind = 'unauthorized' | 'forbidden' | 'http' | 'network' | 'config';

export class ApiError extends Error {
  public readonly status?: number;
  public readonly kind: ApiErrorKind;

  public constructor(message: string, kind: ApiErrorKind, status?: number) {
    super(message);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = status;
  }
}

export interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  body?: unknown;
  token?: string | null;
  headers?: HeadersInit;
}

interface ProblemDetailsLike {
  title?: string;
  detail?: string;
}

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

function getBaseUrl(): string {
  if (import.meta.env.DEV) {
    // In development, route through the Vite proxy (/api) so login works even
    // when no local .env is present.
    return '/api';
  }

  if (!configuredBaseUrl) {
    throw new ApiError('Missing VITE_API_BASE_URL.', 'config');
  }

  return configuredBaseUrl.replace(/\/+$/, '');
}

function buildUrl(path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${getBaseUrl()}${normalizedPath}`;
}

async function parseResponseBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.toLowerCase().includes('application/json')) {
    return null;
  }

  try {
    return await response.json();
  } catch {
    return null;
  }
}

function extractProblemMessage(body: unknown, fallback: string): string {
  if (!body || typeof body !== 'object') {
    return fallback;
  }

  const problem = body as ProblemDetailsLike;
  if (problem.detail) {
    return problem.detail;
  }

  if (problem.title) {
    return problem.title;
  }

  return fallback;
}

export async function apiRequest<TResponse>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<TResponse> {
  const headers = new Headers(options.headers);

  if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }

  if (options.token) {
    headers.set('Authorization', `Bearer ${options.token}`);
  }

  try {
    const response = await fetch(buildUrl(path), {
      method: options.method ?? 'GET',
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    });

    const body = await parseResponseBody(response);

    if (!response.ok) {
      if (response.status === 401) {
        throw new ApiError(
          extractProblemMessage(body, 'Unauthorized request.'),
          'unauthorized',
          response.status,
        );
      }

      if (response.status === 403) {
        throw new ApiError(
          extractProblemMessage(body, 'Forbidden request.'),
          'forbidden',
          response.status,
        );
      }

      throw new ApiError(
        extractProblemMessage(body, `Request failed with status ${response.status}.`),
        'http',
        response.status,
      );
    }

    return body as TResponse;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    throw new ApiError('Network request failed.', 'network');
  }
}
