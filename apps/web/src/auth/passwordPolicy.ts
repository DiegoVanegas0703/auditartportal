export const PASSWORD_MIN_LENGTH = 10

export const PASSWORD_REQUIREMENTS =
  'Mínimo 10 caracteres, con al menos una letra y un número.'

export function passwordPolicyError(password: string): string | null {
  if (password.length < PASSWORD_MIN_LENGTH) {
    return `La contraseña debe tener al menos ${PASSWORD_MIN_LENGTH} caracteres.`
  }
  if (!/\p{L}/u.test(password) || !/\d/.test(password)) {
    return 'La contraseña debe incluir al menos una letra y un número.'
  }
  return null
}
