import type { AuthResponse } from "@/types/auth";
import { createContext } from "react";

export interface AuthContextType {
  user: AuthResponse | null;
  loading: boolean;
  loginWithToken: (token: string) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType | null>(null);
