export type User = {
  id: number;
  name: string;
  email: string;
  role?: string;
};

export type LoginResponse = {
  message: string;
  token: string;
  refreshToken: string;
  user: User;
};

export type MeResponse = {
  id: number;
  name: string;
  email: string;
  role?: string;
};
