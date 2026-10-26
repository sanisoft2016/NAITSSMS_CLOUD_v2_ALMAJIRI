// Production: call /api on whatever host the user opened (apex or www).
// Nginx (or IIS ARR) already proxies /api to the .NET Core API on the same site,
// so the browser stays same-origin and CORS is not required.
// serverUrl must remain an absolute URL: media helpers use new URL(serverUrl).origin.
export const environment = {
  production: true,
  fileUploadMaxSize: 1048576,
  allowedUploadExtentions: ['pdf', 'docx', 'doc', 'png', 'jpg', 'jpeg'],

  get serverUrl(): string {
    return `${new URL(window.location.href).origin}/api`;
  },

  get baseURL(): string {
    return new URL(window.location.href).origin;
  },
  useHash: false,
};
