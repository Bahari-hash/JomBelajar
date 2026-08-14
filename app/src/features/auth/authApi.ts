import { httpClient } from "@/services/httpClient";
import { withRefreshLock } from "@/features/auth/refreshLock";
import type {
  AvatarPresignRequest,
  AuthTokenResponse,
  CurrentUserProfile,
  MediaResourceResponse,
  PresignResponse,
  UpdateProfileRequest,
  UserResponse,
} from "@/features/auth/types";

/** Defines the consumer authentication and current-profile HTTP contract. */
export const authApi = {
  requestRegisterToken: (email: string) =>
    httpClient.post<void>("/auth/register-token", { email }),
  register: (email: string, password: string, verificationCode: string) =>
    httpClient.post<UserResponse>("/auth/register", {
      email,
      password,
      verificationCode,
    }),
  login: (email: string, password: string) =>
    httpClient.post<AuthTokenResponse>(
      "/auth/login",
      { email, password },
      { withCredentials: true },
    ),
  refresh: () =>
    httpClient.post<AuthTokenResponse>(
      "/auth/refresh",
      {},
      { skipAuth: true, withCredentials: true },
    ),
  logout: () =>
    httpClient.post<void>(
      "/auth/logout",
      {},
      { withCredentials: true },
    ),
  getCurrentProfile: () => httpClient.get<CurrentUserProfile>("/users/me"),
  updateProfile: (request: UpdateProfileRequest) =>
    httpClient.put<CurrentUserProfile>("/users/me/profile", request),
  presignAvatar: (request: AvatarPresignRequest) =>
    httpClient.post<PresignResponse>("/uploads/users/avatar/presign", request),
  uploadToPresignedUrl: (
    presignedUrl: string,
    file: File,
    onProgress?: (progress: number) => void,
  ) =>
    httpClient.put<void>(presignedUrl, file, {
      skipAuth: true,
      headers: { "Content-Type": file.type },
      onUploadProgress: (event) => {
        if (event.total && onProgress) {
          onProgress(Math.round((event.loaded / event.total) * 100));
        }
      },
    }),
  confirmUpload: (resourceId: string) =>
    httpClient.put<MediaResourceResponse>(
      `/uploads/resources/${resourceId}/confirm`,
    ),
};

export async function refreshAuthSession() {
  const response = await withRefreshLock(() => authApi.refresh());
  return response.data;
}
