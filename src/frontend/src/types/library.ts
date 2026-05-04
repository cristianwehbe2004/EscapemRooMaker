export type LibraryRoomListItemDto = {
  roomId: string;
  name: string;
  description: string;
  createdAtUtc: string;
  ratingCount: number;
  averageRating: number;
  viewerRating?: number | null;
};

export type LibraryRoomsResponse = {
  items: LibraryRoomListItemDto[];
  page: number;
  pageSize: number;
  total: number;
};

export type UpsertRoomRatingResponse = {
  roomId: string;
  score: number;
  ratingCount: number;
  averageRating: number;
};
