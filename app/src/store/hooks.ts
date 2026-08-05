import { useDispatch, useSelector } from "react-redux";
import type { AppDispatch, RootState } from "@/store/store";

/** Typed Redux hooks shared by the consumer application. */
export function useAppDispatch() {
  return useDispatch<AppDispatch>();
}

export function useAuthState() {
  return useSelector((state: RootState) => state.auth);
}
