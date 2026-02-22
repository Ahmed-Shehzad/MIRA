export interface OrderRoundResponse {
  id: number;
  restaurantName: string;
  restaurantUrl: string | null;
  createdByUserId: string;
  deadline: string;
  status: string;
  itemCount?: number;
}

export interface OrderItemResponse {
  id: number;
  orderRoundId: number;
  userId: string;
  userEmail: string;
  description: string;
  price: number;
  notes: string | null;
}

export interface OrderRoundDetailResponse {
  id: number;
  restaurantName: string;
  restaurantUrl: string | null;
  createdByUserId: string;
  createdByUserEmail: string;
  deadline: string;
  status: string;
  items: OrderItemResponse[];
}
