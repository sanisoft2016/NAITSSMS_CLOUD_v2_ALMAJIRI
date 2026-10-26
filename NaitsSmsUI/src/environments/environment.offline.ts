// Build used by the offline demo stack. The API is reached through the same nginx that serves this
// bundle, so the laptop needs no network at all. serverUrl has to stay an absolute URL: several
// components derive the media origin with `new URL(environment.serverUrl).origin`.
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
