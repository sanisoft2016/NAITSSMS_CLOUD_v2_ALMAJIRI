export * from './noop-interceptor';
export * from './base-url-interceptor';
export * from './settings-interceptor';
export * from './token-interceptor';
export * from './no-cache-interceptor';
export * from './api-interceptor';
export * from './error-interceptor';
export * from './logging-interceptor';

import { apiInterceptor } from './api-interceptor';
import { baseUrlInterceptor } from './base-url-interceptor';
import { errorInterceptor } from './error-interceptor';
import { loggingInterceptor } from './logging-interceptor';
import { noCacheInterceptor } from './no-cache-interceptor';
import { noopInterceptor } from './noop-interceptor';
import { settingsInterceptor } from './settings-interceptor';
import { tokenInterceptor } from './token-interceptor';
import { environment } from '@env/environment';

// Http interceptor providers in outside-in order
const baseInterceptors = [
  noopInterceptor,
  baseUrlInterceptor,
  settingsInterceptor,
  tokenInterceptor,
  noCacheInterceptor,
  apiInterceptor,
  errorInterceptor,
];

export const interceptors = environment.production
  ? baseInterceptors
  : [...baseInterceptors, loggingInterceptor];
