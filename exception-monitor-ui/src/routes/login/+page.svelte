<script lang="ts">
	import { page } from '$app/stores';
	import { isAuthenticated } from '$lib/stores/auth';
	import { settings } from '$lib/stores/settings';
	import { goto } from '$app/navigation';
	import { Bug } from '@lucide/svelte';
	import { onMount } from 'svelte';

	let error = $derived($page.url.searchParams.get('error'));

	const errorMessages: Record<string, string> = {
		token_exchange_failed: 'Failed to complete Google sign-in. Please try again.',
		userinfo_failed: 'Could not retrieve your Google account info.',
		invalid_state: 'Invalid OAuth state. Please try again.',
		missing_params: 'Incomplete OAuth response. Please try again.',
		access_denied: 'Access was denied. Please allow access to continue.'
	};

	onMount(() => {
		if ($isAuthenticated) goto('/');
	});

	function signInWithGoogle() {
		window.location.href = `${$settings.apiBaseUrl}/api/auth/google`;
	}
</script>

<svelte:head><title>Sign In — Exception Monitor</title></svelte:head>

<div class="min-h-screen bg-gray-50 flex items-center justify-center px-4">
	<div class="w-full max-w-sm">
		<!-- Logo -->
		<div class="flex items-center justify-center gap-3 mb-8">
			<div class="p-2 bg-indigo-600 rounded-lg">
				<Bug size={24} class="text-white" />
			</div>
			<div>
				<p class="text-gray-900 font-bold text-xl leading-tight">Exception Monitor</p>
				<p class="text-gray-400 text-sm leading-tight">Admin Dashboard</p>
			</div>
		</div>

		<!-- Card -->
		<div class="bg-white rounded-2xl border border-gray-200 shadow-sm p-8">
			<h1 class="text-xl font-semibold text-gray-900 text-center mb-2">Welcome back</h1>
			<p class="text-gray-500 text-sm text-center mb-6">Sign in to manage your applications and errors</p>

			{#if error}
				<div class="mb-4 px-4 py-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
					{errorMessages[error] ?? 'An error occurred. Please try again.'}
				</div>
			{/if}

			<button
				onclick={signInWithGoogle}
				class="w-full flex items-center justify-center gap-3 px-4 py-3 border border-gray-300 rounded-lg bg-white hover:bg-gray-50 transition-colors text-sm font-medium text-gray-700 shadow-sm"
			>
				<!-- Google icon -->
				<svg width="18" height="18" viewBox="0 0 18 18">
					<path fill="#4285F4" d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844c-.209 1.125-.843 2.078-1.796 2.717v2.258h2.908c1.702-1.567 2.684-3.874 2.684-6.615z"/>
					<path fill="#34A853" d="M9 18c2.43 0 4.467-.806 5.956-2.184l-2.908-2.258c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332A8.997 8.997 0 0 0 9 18z"/>
					<path fill="#FBBC05" d="M3.964 10.707A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.707V4.961H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.039l3.007-2.332z"/>
					<path fill="#EA4335" d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.961L3.964 7.293C4.672 5.163 6.656 3.58 9 3.58z"/>
				</svg>
				Sign in with Google
			</button>
		</div>

		<p class="text-center text-xs text-gray-400 mt-6">
			Access is restricted to authorised users only.
		</p>
	</div>
</div>
