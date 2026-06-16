<!DOCTYPE html>
<html lang="en" dir="ltr">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>{{ $title ?? 'Admin' }} — {{ config('app.name') }}</title>
    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="min-h-screen bg-gray-100 text-gray-900 antialiased">
    @auth
    <div class="flex min-h-screen">
        <aside class="hidden w-60 shrink-0 border-e border-gray-200 bg-white p-5 md:block">
            <div class="mb-6 text-lg font-bold text-brand-700">ريال · Admin</div>
            <nav class="space-y-1 text-sm">
                @php($nav = [
                    'admin.dashboard' => 'Dashboard',
                    'admin.posts.index' => 'Posts',
                    'admin.categories' => 'Categories',
                    'admin.tags' => 'Tags',
                    'admin.settings' => 'Settings',
                ])
                @foreach ($nav as $route => $label)
                    <a href="{{ route($route) }}"
                        class="block rounded-lg px-3 py-2 {{ request()->routeIs($route.'*') || request()->routeIs(str_replace('.index','',$route).'*') ? 'bg-brand-50 font-medium text-brand-700' : 'text-gray-600 hover:bg-gray-50' }}">
                        {{ $label }}
                    </a>
                @endforeach
            </nav>
            <form action="{{ route('admin.logout') }}" method="post" class="mt-6">
                @csrf
                <button class="text-sm text-gray-500 hover:text-red-600">Log out ({{ auth()->user()->name }})</button>
            </form>
            <a href="{{ route('home', ['locale' => config('blog.default_locale')]) }}" class="mt-4 block text-xs text-gray-400 hover:text-brand-700">↗ View site</a>
        </aside>

        <main class="flex-1 p-6 md:p-10">
            @if (session('status'))
                <div class="mb-6 rounded-lg bg-brand-50 px-4 py-3 text-brand-800">{{ session('status') }}</div>
            @endif
            @if ($errors->any())
                <div class="mb-6 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-700">
                    <ul class="list-disc ps-5">@foreach ($errors->all() as $e)<li>{{ $e }}</li>@endforeach</ul>
                </div>
            @endif
            @yield('admin')
        </main>
    </div>
    @else
        @yield('admin')
    @endauth
</body>
</html>
