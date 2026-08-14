import axios from "axios";
import { toApiError } from "@/services/problemDetails.js";

const DEFAULT_API_BASE_URL = "/api";

function normalizeBaseUrl(value) {
  const baseUrl =
    typeof value === "string" && value.trim()
      ? value.trim()
      : DEFAULT_API_BASE_URL;
  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
}

const API_BASE_URL = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL);

function validatePath(path) {
  if (typeof path !== "string" || !path.startsWith("/")) {
    throw new Error("API request paths must start with a slash.");
  }
}

/** Shared Axios instance for all TinyLang administrator HTTP requests. */
export const httpClient = axios.create({
  baseURL: API_BASE_URL,
  headers: { Accept: "application/json" },
});

/** Executes TinyLang API requests without exposing raw error bodies to callers. */
export async function requestApi({
  path,
  method = "GET",
  body,
  accessToken,
  signal,
  withCredentials = false,
}) {
  validatePath(path);

  try {
    const response = await httpClient.request({
      url: path,
      method,
      data: body,
      headers: accessToken
        ? { Authorization: `Bearer ${accessToken}` }
        : undefined,
      withCredentials,
      signal,
    });

    if (response.status === 204) {
      return undefined;
    }

    const contentType = response.headers?.get?.("content-type") ?? "";
    if (
      !contentType.includes("application/json") &&
      !contentType.includes("+json")
    ) {
      return undefined;
    }

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}
