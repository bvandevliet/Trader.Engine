import { csrfHeaders } from './csrf';

// Thrown only for a non-2xx response whose body carries the { error: string } shape every
// Razor Page AJAX handler in this app returns on failure (see Dashboard.cshtml.cs's
// ExecuteExchangeCall and OnPostSaveAsync) — callers can catch this specifically to show the
// server's message, and let anything else (network failure, bad JSON) surface as a generic error.
export class ApiError extends Error {}

export async function postJson<T = void> (url: string, body: unknown): Promise<T>
{
  const response = await fetch(url, {
    method: 'POST',
    headers: csrfHeaders(),
    body: JSON.stringify(body),
  });

  // Handlers that have nothing to return (e.g. OnPostSaveAsync) send an empty 200 body — reading
  // as text first avoids response.json() throwing on it, while still parsing normally otherwise.
  const text = await response.text();
  const data = text ? JSON.parse(text) : undefined;

  if (!response.ok)
  {
    throw new ApiError(data?.error ?? `Request to ${url} failed with status ${response.status}.`);
  }

  return data as T;
}