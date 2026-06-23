<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { authToken } from '$lib/stores/auth';

	onMount(() => {
		const token = $page.url.searchParams.get('token');
		if (token) {
			authToken.set(token);
			goto('/');
		} else {
			goto('/login?error=missing_token');
		}
	});
</script>

<div class="min-h-screen bg-gray-50 flex items-center justify-center">
	<div class="flex items-center gap-2 text-gray-500 text-sm">
		<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
		Signing you in…
	</div>
</div>
