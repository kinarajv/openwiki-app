export const getBackendUrl = (): string => {
  return import.meta.env.VITE_API_URL || 'http://localhost:5083';
};
