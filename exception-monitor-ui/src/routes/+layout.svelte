<script lang="ts">
	import '../app.css';
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import Sidebar from '$lib/components/Sidebar.svelte';
	import Toast from '$lib/components/Toast.svelte';
	import { isAuthenticated } from '$lib/stores/auth';
	import type { Snippet } from 'svelte';

	let { children }: { children: Snippet } = $props();

	const publicRoutes = ['/login', '/auth/callback'];
	let isPublic = $derived(publicRoutes.some((r) => $page.url.pathname.startsWith(r)));

	$effect(() => {
		if (!isPublic && !$isAuthenticated) {
			goto('/login');
		}
	});
</script>

{#if isPublic}
	{@render children()}
{:else if $isAuthenticated}
	<div class="flex h-screen bg-gray-50 overflow-hidden">
		<Sidebar />
		<div class="flex-1 flex flex-col min-w-0 overflow-hidden">
			<main class="flex-1 overflow-auto">
				{@render children()}
			</main>
		</div>
		<Toast />
	</div>
{:else}
	<!-- Brief blank while redirecting to /login -->
	<div class="min-h-screen bg-gray-50"></div>
{/if}
