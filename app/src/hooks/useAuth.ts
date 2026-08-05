import { use } from "react";
import { AuthContext } from "@/providers/authContext";

/** Provides the public authentication state and commands. */
export function useAuth() {
  const context = use(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return context;
}
