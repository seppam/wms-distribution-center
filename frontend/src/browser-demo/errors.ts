/** Mirrors Infrastructure/Exceptions.cs and the ASP.NET status results: each carries the HTTP status the backend would answer with. */
export class HttpError extends Error {
  constructor(public status: number, message: string, public extra?: Record<string, unknown>) { super(message); }
}
/** Resource does not exist (404). */
export class NotFoundError extends HttpError { constructor(message = '') { super(404, message); } }
/** Business rule violated (400). */
export class BusinessRuleError extends HttpError { constructor(message: string) { super(400, message); } }
/** State conflict, duplicate or still referenced (409). */
export class ConflictError extends HttpError { constructor(message: string) { super(409, message); } }
export class UnauthorizedError extends HttpError { constructor(message = '') { super(401, message); } }
export class ForbiddenError extends HttpError { constructor() { super(403, ''); } }

export const REFERENCED_MESSAGE = 'The change conflicts with existing data (duplicate or still referenced).';

/** ASP.NET model validation failure: { title, errors: { Field: [messages] } }. */
export class ValidationError extends HttpError {
  constructor(public errors: Record<string, string[]>) {
    super(400, 'One or more validation errors occurred.', { errors });
  }
}
