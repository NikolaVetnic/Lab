import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react';
import { login as loginRequest, type LoginResponse } from '../api/authApi';

const ACCESS_TOKEN_STORAGE_KEY = 'operationscenter.access_token';

interface LoginInput {
  email: string;
  password: string;
}

interface AuthContextValue {
  accessToken: string | null;
  isAuthenticated: boolean;
  login: (input: LoginInput) => Promise<LoginResponse>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function getInitialToken(): string | null {
  return sessionStorage.getItem(ACCESS_TOKEN_STORAGE_KEY);
}

export function AuthProvider({ children }: PropsWithChildren): JSX.Element {
  const [accessToken, setAccessToken] = useState<string | null>(getInitialToken);

  const login = useCallback(async (input: LoginInput): Promise<LoginResponse> => {
    const response = await loginRequest(input);
    sessionStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, response.accessToken);
    setAccessToken(response.accessToken);
    return response;
  }, []);

  const logout = useCallback(() => {
    sessionStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
    setAccessToken(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      accessToken,
      isAuthenticated: Boolean(accessToken),
      login,
      logout,
    }),
    [accessToken, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.');
  }

  return context;
}
