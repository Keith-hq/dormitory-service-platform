const TECHNICAL_MESSAGE = /request failed with status code|network error|timeout|failed to fetch/i

export const toUserMessage = (error, fallback) => {
  const message = typeof error?.message === 'string' ? error.message.trim() : ''
  return message && !TECHNICAL_MESSAGE.test(message) ? message : fallback
}
