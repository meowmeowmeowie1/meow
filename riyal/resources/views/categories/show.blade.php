@extends('layouts.app')

@section('content')
    <header class="mb-8">
        <p class="text-sm font-semibold uppercase tracking-wide text-brand-700">{{ __('messages.in_category') }}</p>
        <h1 class="mt-1 text-3xl font-extrabold text-gray-900">{{ $category->name }}</h1>
        @if ($category->description)
            <p class="mt-2 max-w-2xl text-gray-600">{{ $category->description }}</p>
        @endif
    </header>

    @if ($posts->isEmpty())
        <p class="rounded-xl border border-dashed border-gray-300 bg-white p-8 text-center text-gray-500">
            {{ __('messages.nothing_here') }}
        </p>
    @else
        <div class="grid gap-6 md:grid-cols-3">
            @foreach ($posts as $post)
                <x-post-card :post="$post" />
            @endforeach
        </div>
        <div class="mt-8">{{ $posts->links() }}</div>
    @endif
@endsection
