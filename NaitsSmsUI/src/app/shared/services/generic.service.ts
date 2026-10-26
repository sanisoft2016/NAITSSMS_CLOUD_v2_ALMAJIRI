import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '@env/environment';
import { UserCredential } from '@shared/models/UserCredential';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';



export enum CurrentClass {
  PlayClass = 0,
  PreNursery = 1,
  Nursery1 = 2,
  Nursery2 = 3,
  Primary1 = 4,
  Primary2 = 5,
  Primary3 = 6,
  Primary4 = 7,
  Primary5 = 8,
  Primary6 = 9,
  Jss1 = 10,
  Jss2 = 11,
  Jss3 = 12,
  Ss1 = 13,
  Ss2 = 14,
  Ss3 = 15,
}
type ClassOption = { value: CurrentClass; label: string };

@Injectable({
  providedIn: 'root',
})
/**
 * Generic CRUD service for any model type T.
 * Includes error handling and support for custom HTTP options.
 */
export class GenericService<T> {
  protected readonly baseUrl = environment.serverUrl;

  constructor(protected http: HttpClient) {}


  readonly classNames: ClassOption[] = [
    { value: CurrentClass.PlayClass, label: 'PlayClass' },
    { value: CurrentClass.PreNursery, label: 'PreNursery' },
    { value: CurrentClass.Nursery1, label: 'Nursery1' },
    { value: CurrentClass.Nursery2, label: 'Nursery2' },
    { value: CurrentClass.Primary1, label: 'Primary1' },
    { value: CurrentClass.Primary2, label: 'Primary2' },
    { value: CurrentClass.Primary3, label: 'Primary3' },
    { value: CurrentClass.Primary4, label: 'Primary4' },
    { value: CurrentClass.Primary5, label: 'Primary5' },
    { value: CurrentClass.Primary6, label: 'Primary6' },
    { value: CurrentClass.Jss1, label: 'Jss1' },
    { value: CurrentClass.Jss2, label: 'Jss2' },
    { value: CurrentClass.Jss3, label: 'Jss3' },
    { value: CurrentClass.Ss1, label: 'Ss1' },
    { value: CurrentClass.Ss2, label: 'Ss2' },
    { value: CurrentClass.Ss3, label: 'Ss3' },
  ];

  /**
   * Get item(s) of type T.
   * @param suffixUrl The endpoint path to append to the baseUrl.
   * @param options Optional HTTP options (params only, headers are set automatically).
   */
  get(suffixUrl: string, options?: { params?: HttpParams }): Observable<T[]>;
  get<R>(suffixUrl: string, options?: { params?: HttpParams }): Observable<R>;
  get(suffixUrl: string, options?: { params?: HttpParams }): Observable<any> {
    const headers = this.getHeaders();
    return this.http
      .get<any>(`${this.baseUrl}${suffixUrl}`, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  getAnObj(suffixUrl: string, options?: { params?: HttpParams }): Observable<T>;
  getAnObj<R>(suffixUrl: string, options?: { params?: HttpParams }): Observable<R>;
  getAnObj(suffixUrl: string, options?: { params?: HttpParams }): Observable<any> {
    const headers = this.getHeaders();
    return this.http
      .get<any>(`${this.baseUrl}${suffixUrl}`, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  /**
   * Create a new item.
   * @param suffixUrl The endpoint path to append to the baseUrl.
   * @param item The item to create.
   * @param options Optional HTTP options (params only, headers are set automatically).
   */
  create(suffixUrl: string, item: T, options?: { params?: HttpParams }): Observable<T>;
  create<R>(suffixUrl: string, item: unknown, options?: { params?: HttpParams }): Observable<R>;
  create(suffixUrl: string, item: unknown, options?: { params?: HttpParams }): Observable<any> {
    const headers = this.getHeaders();
    return this.http
      .post<any>(`${this.baseUrl}${suffixUrl}`, item, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  /**
   * Create a new item with file upload (multipart/form-data).
   * @param suffixUrl The endpoint path to append to the baseUrl.
   * @param formData FormData object containing fields and files.
   * @param options Optional HTTP options (params only, headers are set automatically).
   */
  createWithFile(
    suffixUrl: string,
    formData: FormData,
    options?: { params?: HttpParams }
  ): Observable<any> {
    const headers = this.getHeaders(true); // true = multipart
    return this.http
      .post<any>(`${this.baseUrl}${suffixUrl}`, formData, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  /**
   * Update an existing item by ID.
   * @param suffixUrl The endpoint path to append to the baseUrl.
   * @param item The item to update.
   * @param options Optional HTTP options (params only, headers are set automatically).
   */
  update(suffixUrl: string, item: T, options?: { params?: HttpParams }): Observable<T>;
  update<R>(suffixUrl: string, item: unknown, options?: { params?: HttpParams }): Observable<R>;
  update(suffixUrl: string, item: unknown, options?: { params?: HttpParams }): Observable<any> {
    const headers = this.getHeaders();
    return this.http
      .put<any>(`${this.baseUrl}${suffixUrl}`, item, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  /**
   * Update an existing item by ID with file upload (multipart/form-data).
   * @param suffixUrl The endpoint path to append to the baseUrl.
   * @param formData FormData object containing fields and files.
   * @param options Optional HTTP options (params only, headers are set automatically).
   */
  updateWithFile(
    suffixUrl: string,
    formData: FormData,
    options?: { params?: HttpParams }
  ): Observable<any> {
    const headers = this.getHeaders(true); // true = multipart
    return this.http
      .put<any>(`${this.baseUrl}${suffixUrl}`, formData, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  /**
   * Delete an item by ID.
   * @param suffixUrl The endpoint path to append to the baseUrl.
   * @param options Optional HTTP options (params only, headers are set automatically).
   */
  delete(suffixUrl: string, options?: { params?: HttpParams }): Observable<void>;
  delete<R>(suffixUrl: string, options?: { params?: HttpParams }): Observable<R>;
  delete(suffixUrl: string, options?: { params?: HttpParams }): Observable<any> {
    const headers = this.getHeaders();
    return this.http
      .delete<any>(`${this.baseUrl}${suffixUrl}`, { ...options, headers })
      .pipe(catchError(this.handleError));
  }

  /**
   * Retrieve HttpHeaders from local storage (e.g., for auth tokens).
   * If isMultipart is true, do not set Content-Type (browser will set it for FormData).
   */
  protected getHeaders(isMultipart = false): HttpHeaders {
    // Authorization is injected automatically by token-interceptor for all
    // requests targeting environment.serverUrl — do not set it here.
    let headers = new HttpHeaders();
    if (!isMultipart) {
      headers = headers.set('Content-Type', 'application/json');
    }
    return headers;
  }

  /** Handle HTTP errors. */
  protected handleError(error: any) {
    // Log actionable detail so failures are diagnosable (status + URL + server body).
    if (error instanceof HttpErrorResponse) {
      console.error(
        `CRUD operation error: HTTP ${error.status} ${error.statusText || ''} @ ${error.url}`,
        { serverBody: error.error, message: error.message }
      );
    } else {
      console.error('CRUD operation error (non-HTTP):', error);
    }
    return throwError(() => error);
  }



public static setUserCredential(credential: UserCredential) {
        return new Promise((resolve) => {
            localStorage.setItem("credential", JSON.stringify(credential));
            resolve(true);
        });
    }

 public static getNotificationObj(color: string): any {
        return {
            type: color,
            timer: 3000,
            placement: {
                from: "top",
                align: "right"
            },
            template: '<div data-notify="container" class="col-xs-11 col-sm-3 alert alert-{0} alert-with-icon" role="alert">' +
                '<button mat-raised-button type="button" aria-hidden="true" class="close" data-notify="dismiss">  <i class="material-icons">close</i></button>' +
                '<i class="material-icons" data-notify="icon">notifications</i> ' +
                '<span data-notify="title">{1}</span> ' +
                '<span data-notify="message">{2}</span>' +
                '<div class="progress" data-notify="progressbar">' +
                '<div class="progress-bar progress-bar-{0}" role="progressbar" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100" style="width: 0%;"></div>' +
                '</div>' +
                '<a href="{3}" target="{4}" data-notify="url"></a>' +
                '</div>'
        }
    }


    getNotificationObj(color: string): any {
        return {
            type: color,
            timer: 3000,
            placement: {
                from: "top",
                align: "right"
            },
            template: '<div data-notify="container" class="col-xs-11 col-sm-3 alert alert-{0} alert-with-icon" role="alert">' +
                '<button mat-raised-button type="button" aria-hidden="true" class="close" data-notify="dismiss">  <i class="material-icons">close</i></button>' +
                '<i class="material-icons" data-notify="icon">notifications</i> ' +
                '<span data-notify="title">{1}</span> ' +
                '<span data-notify="message">{2}</span>' +
                '<div class="progress" data-notify="progressbar">' +
                '<div class="progress-bar progress-bar-{0}" role="progressbar" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100" style="width: 0%;"></div>' +
                '</div>' +
                '<a href="{3}" target="{4}" data-notify="url"></a>' +
                '</div>'
        }
    }
}
