const EMAIL_MAX_LENGTH = 100;
const PASSWORD_MIN_LENGTH = 8;
const PASSWORD_MAX_LENGTH = 50;

/** Mirrors browser-checkable authentication constraints from the API validators. */
export function validateEmail(value: string) {
  const email = value.trim();
  if (!email) {
    return "请输入邮箱。";
  }
  if (email.length > EMAIL_MAX_LENGTH) {
    return `邮箱不能超过 ${EMAIL_MAX_LENGTH} 个字符。`;
  }
  if (!/^\S+@\S+\.\S+$/.test(email)) {
    return "请输入有效的邮箱地址。";
  }
  return null;
}

export function validateVerificationCode(value: string) {
  if (!value) {
    return "请输入验证码。";
  }
  return /^\d{6}$/.test(value) ? null : "验证码必须是 6 位数字。";
}

export function validateLoginPassword(value: string) {
  if (!value) {
    return "请输入密码。";
  }
  return value.length <= PASSWORD_MAX_LENGTH
    ? null
    : `密码不能超过 ${PASSWORD_MAX_LENGTH} 个字符。`;
}

export function validateNewPassword(value: string) {
  if (!value) {
    return "请输入新密码。";
  }
  if (value.length < PASSWORD_MIN_LENGTH) {
    return `密码至少需要 ${PASSWORD_MIN_LENGTH} 个字符。`;
  }
  if (value.length > PASSWORD_MAX_LENGTH) {
    return `密码不能超过 ${PASSWORD_MAX_LENGTH} 个字符。`;
  }
  return null;
}

export function validatePasswordConfirmation(
  password: string,
  confirmation: string,
) {
  if (!confirmation) {
    return "请再次输入密码。";
  }
  return password === confirmation ? null : "两次输入的密码不一致。";
}
