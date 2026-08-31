// Mirrors InventoryService.Application/Contracts/ProductDtos.cs field-for-field.
// The frontend only ever reads products (picker + stock check) - create/
// update-stock are modeled for completeness but unused; product management
// is Admin Dashboard bonus scope, out of scope by deliberate decision.

export interface CreateProductRequest {
  sku: string;
  name: string;
  description?: string | null;
  unitPrice: number;
  stockQuantity: number;
}

export interface UpdateStockRequest {
  stockQuantity: number;
}

export interface ProductResponse {
  id: string;
  sku: string;
  name: string;
  description: string | null;
  unitPrice: number;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
