@extends('admin.layout')

@section('admin')
<div class="grid min-h-screen place-items-center p-4">
    <div class="w-full max-w-sm rounded-2xl border border-gray-200 bg-white p-8 shadow-sm">
        <div class="mb-6 text-center text-2xl font-bold text-brand-700">ريال · Admin</div>

        @if ($errors->any())
            <div class="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-700">{{ $errors->first() }}</div>
        @endif

        <form action="{{ route('admin.login.attempt') }}" method="post" class="space-y-4">
            @csrf
            <div>
                <label class="mb-1 block text-sm font-medium text-gray-700">Email</label>
                <input type="email" name="email" value="{{ old('email') }}" required autofocus
                    class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
            </div>
            <div>
                <label class="mb-1 block text-sm font-medium text-gray-700">Password</label>
                <input type="password" name="password" required
                    class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
            </div>
            <label class="flex items-center gap-2 text-sm text-gray-600">
                <input type="checkbox" name="remember"> Remember me
            </label>
            <button class="w-full rounded-lg bg-brand-600 px-4 py-2.5 font-medium text-white hover:bg-brand-700">
                Sign in
            </button>
        </form>
    </div>
</div>
@endsection
