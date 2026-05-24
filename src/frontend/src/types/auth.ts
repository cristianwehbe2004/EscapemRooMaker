export type UserProfile = {
  id: string;
  username: string;
  email: string;
  role: string;
};

export type AuthResponse = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  user: UserProfile;
};

export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = {
  username: string;
  email: string;
  password: string;
  role: string;
};

export type StoredAuthSession = AuthResponse;
