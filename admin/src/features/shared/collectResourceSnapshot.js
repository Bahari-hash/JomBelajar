/** Collects an unfiltered snapshot before confirmation; never deletes newly arriving rows. */
export async function collectResourceSnapshot(loadPage) {
  const items = new Map();
  let totalPages = 1;
  for (let page = 1; page <= totalPages; page++) {
    const result = await loadPage(page);
    totalPages = result.totalPages;
    for (const item of result.items) items.set(item.id, item);
  }
  return [...items.values()];
}
