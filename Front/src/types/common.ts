export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ValidationProblem {
  type: string;
  title: string;
  status: number;
  errors: Record<string, string[]>;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
}
