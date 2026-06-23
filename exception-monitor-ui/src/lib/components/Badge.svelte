<script lang="ts">
	let { value }: { value: string } = $props();

	const colorMap: Record<string, string> = {
		// Severity
		Critical: 'bg-red-100 text-red-700',
		Error: 'bg-orange-100 text-orange-700',
		Warning: 'bg-yellow-100 text-yellow-700',
		Info: 'bg-blue-100 text-blue-700',
		Debug: 'bg-gray-100 text-gray-500',
		// Status
		Open: 'bg-red-100 text-red-700',
		Acknowledged: 'bg-yellow-100 text-yellow-800',
		Resolved: 'bg-green-100 text-green-700',
		Ignored: 'bg-gray-100 text-gray-500'
	};

	let cls = $derived(colorMap[value] ?? 'bg-gray-100 text-gray-600');
</script>

<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium {cls}">
	{value}
</span>
