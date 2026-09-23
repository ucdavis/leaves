export function getDirectorySearchMessage(
  query: string,
  isSearching: boolean,
  searchFailed: boolean,
  resultCount: number
): string | null {
  if (query.trim().length < 2) {
    return 'Type at least 2 characters.';
  }
  if (isSearching) {
    return 'Searching...';
  }
  if (searchFailed) {
    return 'Could not search people. Try again.';
  }
  if (resultCount === 0) {
    return 'No people match that search.';
  }
  if (resultCount === 20) {
    return 'Showing up to 20 matches. Refine your search if needed.';
  }
  return null;
}
