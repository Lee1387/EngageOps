import { isRecord } from './json'

export function getValidationProblemFieldErrors(
  responseBody: unknown,
  fieldName: string,
): string[] | null {
  if (!isRecord(responseBody) || !isRecord(responseBody.errors)) {
    return null
  }

  const fieldErrors = responseBody.errors[fieldName]

  return Array.isArray(fieldErrors) &&
    fieldErrors.length > 0 &&
    fieldErrors.every((error) => typeof error === 'string')
    ? fieldErrors
    : null
}
