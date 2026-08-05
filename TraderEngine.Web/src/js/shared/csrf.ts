export function csrfHeaders (): HeadersInit
{
  const token = document.querySelector<HTMLMetaElement>('meta[name=\'csrf-token\']')?.content ?? '';

  return {
    'Content-Type': 'application/json',
    RequestVerificationToken: token,
  };
}