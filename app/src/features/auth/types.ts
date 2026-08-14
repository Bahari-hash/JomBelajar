export type UserRole = "User" | "Admin";

export interface UserResponse {
  id: string;
  email: string;
  role: UserRole;
}

export interface AuthTokenResponse {
  token: string;
  expiresIn: number;
  user: UserResponse;
}

export interface CurrentUserProfile {
  id: string;
  email: string;
  role: UserRole;
  nickname: string | null;
  avatarUrl: string | null;
  bio: string | null;
  createdAt: string;
}

export interface UpdateProfileRequest {
  nickname: string | null;
  avatarMediaResourceId?: string | null;
  bio: string | null;
}

export interface AvatarPresignRequest {
  originalName: string;
  extension: string;
  contentType: string;
  size: number;
}

export interface PresignResponse {
  resourceId: string;
  presignedUrl: string;
  objectName: string;
}

export interface MediaResourceResponse {
  id: string;
  uploaderId: string;
  objectName: string;
  originalName: string;
  module: string;
  status: string;
  size: number;
  extension: string;
  contentType: string;
  url: string | null;
  createdAt: string;
}
