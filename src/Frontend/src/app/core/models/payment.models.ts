// Mirrors PaymentService.Application/Contracts/PaymentDtos.cs field-for-field.

export type PaymentStatus = 'Pending' | 'Processing' | 'Succeeded' | 'Failed' | 'Refunded';

export interface ProcessPaymentRequest {
  orderId: string;
  amount: number;
  paymentMethod?: string | null;
}

export interface PaymentResponse {
  id: string;
  orderId: string;
  amount: number;
  paymentMethod: string;
  status: PaymentStatus;
  transactionId: string | null;
  failureReason: string | null;
  createdAt: string;
  updatedAt: string;
}
